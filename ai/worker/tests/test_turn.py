"""한 턴 구조 (/v1/npc/reply): 턴 전체 음성(audio_full_id, 다시 듣기용)과 스트림 안의 추천 단어(words 이벤트).
가짜 공급자·가짜 음성 합성이라 진짜 호출 없음."""

import io
import json
import wave

import httpx

from conftest import FakeTts, make_client, ok, stream

NPC = {"id": "mia", "name": "Mia", "role": "the barista.", "facts": "Menu: latte $5.", "scene": "Cafe."}
HISTORY = [{"who": "npc", "text": "Hi! What can I get for you?"}]


def events(resp) -> list[tuple[str, dict]]:
    out = []
    for block in resp.text.strip().split("\n\n"):
        lines = dict(line.split(": ", 1) for line in block.splitlines() if ": " in line)
        out.append((lines["event"], json.loads(lines["data"])))
    return out


def frames(wav: bytes) -> int:
    with wave.open(io.BytesIO(wav)) as w:
        return w.getnframes()


def reply(provider, engine, **body):
    provider.answers["npc"] = lambda b: stream(["Sure! ", "One latte. ", "Anything else?"])
    with make_client(provider, engine) as c:
        r = c.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "one latte"} | body)
        ev = events(r)
        done = ev[-1][1] if ev[-1][0] == "done" else None
        full = c.get(f"/v1/audio/{done['audio_full_id']}") if done and done.get("audio_full_id") else None
    return ev, done, full


# ---------------------------------------------------------------------
# 턴 전체 음성
# ---------------------------------------------------------------------
def test_done_has_whole_turn_audio_joined_from_chunks(provider):
    tts = FakeTts(wav=True)
    ev, done, full = reply(provider, tts)
    chunks = [d for name, d in ev if name == "audio"]
    assert len(chunks) == 3 and all(c["tts"] for c in chunks)
    assert full is not None and full.status_code == 200 and full.headers["content-type"] == "audio/wav"
    assert frames(full.content) == sum(len(c["text"]) * 100 for c in chunks)  # 조각을 순서대로 이어 붙인 길이
    assert done["audio_full_s"] == round(frames(full.content) / 24000, 2)


def test_no_whole_turn_audio_when_a_chunk_failed(provider):
    """조각 하나라도 합성하지 못하면 다시 듣기용 음성은 없음 (Spring은 /v1/tts로 다시 만든다)."""
    ev, done, _ = reply(provider, FakeTts(wav=True, fail_on="One latte."))
    assert [d["tts"] for name, d in ev if name == "audio"] == [True, False, True]
    assert done["audio_full_id"] is None and done["audio_full_s"] is None


def test_late_chunks_still_make_whole_turn_audio(provider):
    """조각 상한(바로 재생용)을 넘긴 조각도 합성이 끝나면 다시 듣기용 음성에 넣는다 (Spring은 done까지 기다린다)."""
    provider.answers["npc"] = lambda b: stream(["Sure! ", "One latte. ", "Anything else?"])
    with make_client(provider, FakeTts(delay=0.5, wav=True), tts_chunk_budget_s=0.1) as c:
        ev = events(c.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "one latte"}))
        chunks = [d for name, d in ev if name == "audio"]
        done = ev[-1][1]
        # 가짜 합성은 조각을 동시에 만들어 뒤 조각은 상한 안에 끝날 수 있다. 첫 조각은 반드시 늦어 바로 재생은 자막만
        assert len(chunks) == 3 and not chunks[0]["tts"]
        assert done["audio_full_id"] is not None
        full = c.get(f"/v1/audio/{done['audio_full_id']}")
        assert frames(full.content) == sum(len(c["text"]) * 100 for c in chunks)


def test_no_whole_turn_audio_when_a_late_chunk_failed(provider):
    provider.answers["npc"] = lambda b: stream(["Sure! ", "One latte. ", "Anything else?"])
    tts = FakeTts(delay=0.5, wav=True, fail_on="One latte.")
    with make_client(provider, tts, tts_chunk_budget_s=0.1) as c:
        done = events(c.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "one latte"}))[-1]
        assert done[0] == "done" and done[1]["audio_full_id"] is None


def test_no_whole_turn_audio_without_tts(provider):
    _, done, _ = reply(provider, FakeTts(wav=True), tts=False)
    assert done["audio_full_id"] is None


def test_slow_full_turn_audio_does_not_hold_the_completed_text(provider):
    class SlowTts(FakeTts):
        completed = False

        async def asynth(self, *args):
            result = await super().asynth(*args)
            self.completed = True
            return result

    tts = SlowTts(delay=0.5, wav=True)
    provider.answers["npc"] = lambda body: stream(["Sure! One latte."])
    with make_client(provider, tts, tts_chunk_budget_s=0.01, tts_full_budget_s=0.02) as client:
        done = events(client.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "one latte"}))[-1]

        assert done[0] == "done" and done[1]["text"] == "Sure! One latte."
        assert tts.completed is False
        assert done[1]["audio_full_id"] is None


# ---------------------------------------------------------------------
# 스트림 안의 추천 단어
# ---------------------------------------------------------------------
def test_words_event_comes_before_done_when_asked(provider):
    seen = {}

    def answer(b):
        seen["body"] = b
        return ok(json.dumps({"words": ["no thanks", "a muffin", "that's all"]}))

    provider.answers["suggest"] = answer
    ev, done, _ = reply(provider, FakeTts(), suggest_words=True, goal="say if they want anything else", level=1)
    names = [name for name, _ in ev]
    assert names.count("words") == 1 and names.index("words") == len(names) - 2  # done 바로 앞
    assert dict(ev)["words"] == {"words": ["no thanks", "a muffin", "that's all"], "error": None}
    msg = seen["body"]["messages"][-1]["content"]
    assert "Sure! One latte. Anything else?" in msg and "say if they want anything else" in msg and "A1-A2" in msg
    assert {u["step"] for u in done["usage"]} == {"npc", "suggest"}


def test_no_words_unless_asked(provider):
    ev, _, _ = reply(provider, FakeTts())
    assert "words" not in [name for name, _ in ev]
    assert ("suggest", "openai/gpt-oss-20b") not in provider.calls


def test_words_failure_does_not_break_the_reply(provider):
    provider.answers["suggest"] = lambda b: httpx.Response(503, text="down")
    ev, done, _ = reply(provider, FakeTts(), suggest_words=True)
    assert dict(ev)["words"] == {"words": None, "error": "provider_unavailable"}
    assert done is not None and done["text"] == "Sure! One latte. Anything else?"


def test_no_words_when_reply_failed(provider):
    provider.answers["npc"] = lambda b: httpx.Response(503, text="down")
    with make_client(provider, FakeTts()) as c:
        ev = events(c.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "hi", "suggest_words": True}))
    assert [name for name, _ in ev] == ["error"]


# ---------------------------------------------------------------------
# 코드 리뷰 #1·#2·#7: 추천 단어가 대답을 늦추거나 끊거나, 실패한 대답에 붙지 않게
# ---------------------------------------------------------------------
def test_slow_words_do_not_hold_done(provider):
    """추천 단어 호출이 멈춰도 WORDS_BUDGET_S가 지나면 words: null(timeout)로 넘기고 done을 보낸다."""
    import asyncio
    import time

    async def slow(b):
        await asyncio.sleep(1.5)
        return ok(json.dumps({"words": ["late"]}))

    provider.answers["suggest"] = slow
    provider.answers["npc"] = lambda b: stream(["Sure! ", "One latte."])
    with make_client(provider, FakeTts(), words_budget_s=0.2) as c:
        t = time.perf_counter()
        ev = events(c.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "one latte", "suggest_words": True}))
        spent = time.perf_counter() - t
    assert dict(ev)["words"] == {"words": None, "error": "timeout"} and ev[-1][0] == "done"
    assert spent < 1.0


def test_unexpected_words_error_still_ends_with_done(provider, monkeypatch):
    from app import services

    async def broken(*a, **kw):
        raise RuntimeError("bug")

    monkeypatch.setattr(services, "suggest", broken)
    ev, done, _ = reply(provider, FakeTts(), suggest_words=True)
    assert dict(ev)["words"] == {"words": None, "error": "internal_error"}
    assert done is not None and done["text"] == "Sure! One latte. Anything else?"


class BreaksAfterLine(httpx.AsyncByteStream):
    """첫 줄(줄바꿈으로 끝남)을 보낸 뒤 연결이 끊기는 스트림."""

    async def __aiter__(self):
        yield (f"data: {json.dumps({'choices': [{'delta': {'content': 'Sure, one latte.' + chr(10) + 'Bob:'}}]})}\n\n").encode()
        raise httpx.ReadError("connection reset")


def test_no_words_when_reply_fails_after_text(provider):
    """줄바꿈으로 글자가 멈춘 뒤 스트림이 끊기면 대답은 error — 이미 시작한 추천 단어도 보내지 않는다."""
    provider.answers["npc"] = lambda b: httpx.Response(200, stream=BreaksAfterLine())
    provider.answers["suggest"] = lambda b: ok(json.dumps({"words": ["thanks"]}))
    with make_client(provider, FakeTts()) as c:
        ev = events(c.post("/v1/npc/reply", json={"npc": NPC, "history": HISTORY, "text": "one latte", "suggest_words": True}))
    names = [name for name, _ in ev]
    assert names[-1] == "error" and "words" not in names
