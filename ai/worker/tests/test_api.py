"""AI 워커 API 테스트 (모델 호출 없음, 몇 초 안에 끝남).   pytest -q   (ai/worker 에서)"""

import asyncio
import json
import logging

import httpx
import pytest

from app.npc import Splitter
from conftest import FakeTts, make_client, ok, stream

NPC = {"id": "emma", "name": "Emma", "role": "the barista at Maple Cafe.", "facts": "Menu: latte $5.", "scene": "Inside Maple Cafe."}
CAFE_STATE = {
    "id": "ordering",
    "desc": "The barista is waiting for the order.",
    "intents": [{"id": "order_items", "desc": "orders"}, {"id": "unclear", "desc": "cannot tell"}],
    "slots": {
        "items": {
            "catalog": ["americano", "latte", "muffin"],
            "iced_ok": ["americano", "latte"],
            "catalog_word": "menu",
            "vague_words": "'coffee' or 'drink'",
        },
        "option": {"slot": "choice", "labels": ["here", "to_go"], "prompt": "where they will have it"},
    },
}
PHARMACY_STATE = {
    "id": "ordering",
    "desc": "The pharmacist is asking what they need.",
    "intents": [{"id": "order_items", "desc": "buys"}, {"id": "describe_symptom", "desc": "symptom"}, {"id": "unclear", "desc": "?"}],
    "slots": {"items": {"catalog": ["pain reliever", "cough syrup"], "catalog_word": "products"}},
}


def judge_body(text, state=CAFE_STATE, **kw):
    return {"npc": NPC, "state": state, "history": [{"who": "npc", "text": "Hi! What can I get for you?"}], "text": text, **kw}


def events(resp) -> list[tuple[str, dict]]:
    out = []
    for block in resp.text.strip().split("\n\n"):
        lines = dict(line.split(": ", 1) for line in block.split("\n"))
        out.append((lines["event"], json.loads(lines["data"])))
    return out


# ---------------------------------------------------------------------
# 공통 규칙
# ---------------------------------------------------------------------
def test_token_required_and_health_open(provider):
    with make_client(provider) as c:
        r = c.post("/v1/judge", json=judge_body("hi"), headers={"X-Internal-Token": "wrong"})
        assert r.status_code == 401 and r.json()["error"]["code"] == "unauthorized"
        assert c.get("/health", headers={"X-Internal-Token": ""}).json()["ok"] is True


def test_successful_health_is_quiet_but_other_requests_are_logged(client, caplog):
    caplog.set_level(logging.INFO, logger="ai_worker")
    caplog.clear()
    assert client.get("/health").status_code == 200
    assert not [record for record in caplog.records if record.name == "ai_worker"]

    assert client.get("/missing").status_code == 404
    request_logs = [json.loads(record.message) for record in caplog.records if record.name == "ai_worker"]
    assert len(request_logs) == 1
    assert request_logs[0]["path"] == "/missing" and request_logs[0]["status"] == 404


def test_request_id_echoed_and_bad_request_format(client):
    r = client.post("/v1/judge", json={"npc": NPC, "text": "hi"}, headers={"X-Request-Id": "req-1"})
    assert r.status_code == 400
    assert r.headers["X-Request-Id"] == "req-1"
    assert r.json()["error"]["code"] == "invalid_request" and "state" in r.json()["error"]["message"]


# ---------------------------------------------------------------------
# 판정
# ---------------------------------------------------------------------
def test_judge_cleans_items_to_catalog(client, provider):
    provider.answers["intent"] = lambda b: ok(
        json.dumps(
            {
                "intent": "order_items",
                "choice": "to_go",
                "items": [
                    {"name": "latte", "iced": True, "qty": 2},
                    {"name": "muffin", "iced": True, "qty": 1},
                    {"name": "pizza", "iced": False, "qty": 1},
                ],
            }
        )
    )
    r = client.post("/v1/judge", json=judge_body("two iced lattes and a muffin to go")).json()
    assert r["intent"] == {
        "id": "order_items",
        "ids": ["order_items"],
        "error": None,
        "slots": {"items": [{"name": "latte", "iced": True, "qty": 2}, {"name": "muffin", "iced": False, "qty": 1}], "choice": "to_go"},
    }
    assert "guard" not in r  # 무례 판정은 /v1/guard가 맡는다
    assert r["translation"] is None  # 한국어가 없으면 번역을 부르지 않음
    assert [u["step"] for u in r["usage"]] == ["intent"]
    assert all(u["input_tokens"] == 100 for u in r["usage"])


def test_intent_schema_follows_place_slots(client, provider):
    """약국처럼 아이스·선택지가 없는 장소는 판정 형식에도 그 칸이 없다 (장소 데이터 형식)."""
    seen = {}

    def intent(b):
        seen["schema"] = b["response_format"]["json_schema"]["schema"]
        seen["prompt"] = b["messages"][1]["content"]
        return ok(json.dumps({"intent": "describe_symptom", "items": []}))

    provider.answers["intent"] = intent
    r = client.post("/v1/judge", json=judge_body("I have a headache", PHARMACY_STATE)).json()
    assert r["intent"]["id"] == "describe_symptom"
    props = seen["schema"]["properties"]
    assert set(props) == {"intents", "items", "events"}
    assert set(props["items"]["items"]["properties"]) == {"name", "qty"}
    assert "products: pain reliever, cough syrup" in seen["prompt"] and "iced" not in seen["prompt"]


def test_several_actions_in_one_line(client, provider):
    """'to go. cash': 포장 고르기 단계에서도 다음 단계 의도(결제)를 이어서 고를 수 있고, 말한 순서대로 돌려준다."""
    seen = {}

    def intent(b):
        seen["schema"] = b["response_format"]["json_schema"]["schema"]
        return ok(json.dumps({"intents": ["choose_here_or_to_go", "pay", "fly"], "items": [], "choice": "to_go"}))

    provider.answers["intent"] = intent
    state = {
        **CAFE_STATE,
        "id": "paying",
        "intents": [{"id": "choose_here_or_to_go", "desc": "here or to go"}, {"id": "unclear", "desc": "?"}],
        "more_intents": [{"id": "pay", "desc": "pays"}],
    }
    r = client.post("/v1/judge", json=judge_body("to go. cash", state)).json()
    assert r["intent"]["ids"] == ["choose_here_or_to_go", "pay"] and r["intent"]["id"] == "choose_here_or_to_go"  # 모르는 의도(fly)는 버림
    assert "pay" in seen["schema"]["properties"]["intents"]["items"]["enum"]


def test_korean_line_gets_translation_without_guard(client, provider):
    """번역 카드를 보여 줄지(무례가 아님이 확정일 때만)는 /v1/guard 결과로 Spring이 정한다. /v1/expressions는 번역만."""
    provider.answers["expressions"] = lambda b: ok(json.dumps({"english_correction": None, "better_expressions": []}))
    provider.answers["translate"] = lambda b: ok("Shut up and give me coffee.")
    r = client.post("/v1/expressions", json=judge_body("닥치고 커피 내놔")).json()
    assert r["translation"] == {"english": "Shut up and give me coffee.", "error": None}
    assert sorted(step for step, _ in provider.calls) == ["expressions", "translate"]


def translated(provider, client, answer: str, text: str = "제 이름은 김사피예요") -> dict:
    provider.answers["expressions"] = lambda b: ok(json.dumps({"english_correction": None, "better_expressions": []}))
    provider.answers["translate"] = lambda b: ok(answer)
    return client.post("/v1/expressions", json=judge_body(text)).json()["translation"]


def test_translation_keeps_a_name_in_double_quotes_at_the_end(client, provider):
    # 영어 철자가 없는 이름(한글 닉네임)은 로마자로 바꾸지 않고 따옴표로 그대로 둔다. 끝 따옴표가 지워지면 안 된다
    assert translated(provider, client, 'My name is "김사피"')["english"] == 'My name is "김사피"'


@pytest.mark.parametrize(
    ("answer", "english"),
    [('"I\'ll just eat here."', "I'll just eat here."), ('"My name is "김사피"."', 'My name is "김사피".')],
)
def test_translation_wrapped_in_quotes_is_unwrapped(client, provider, answer, english):
    assert translated(provider, client, answer)["english"] == english


def test_translation_prompt_keeps_names_without_an_english_spelling_as_said(client, provider):
    seen = {}

    def answer(b):
        seen["system"] = b["messages"][0]["content"]
        return ok('My name is "김사피".')

    provider.answers["expressions"] = lambda b: ok(json.dumps({"english_correction": None, "better_expressions": []}))
    provider.answers["translate"] = answer
    client.post("/v1/expressions", json=judge_body("제 이름은 김사피예요"))
    assert "never romanize" in seen["system"] and 'My name is "김사피".' in seen["system"]


def test_action_only_is_dropped_without_model_call(client, provider):
    r = client.post("/v1/judge", json=judge_body("*총을 꺼낸다*")).json()
    assert r["dropped"] is True and "행동 묘사" in r["notice"]
    assert provider.calls == []


def test_action_removed_from_mixed_line(client, provider):
    provider.answers["intent"] = lambda b: ok(
        json.dumps({"intent": "order_items", "items": [{"name": "latte", "iced": False, "qty": 1}], "choice": "none"})
    )
    r = client.post("/v1/judge", json=judge_body("*waves* one latte please")).json()
    assert r["text"] == "one latte please" and r["notice"] is not None


def test_intent_failure_keeps_turn_going(client, provider):
    """모델이 대체 모델까지 모두 실패해도 판정은 200: 의도는 unclear + error (턴은 이어 감)."""
    provider.answers["intent"] = lambda b: httpx.Response(503, text="down")
    r = client.post("/v1/judge", json=judge_body("hello"))
    assert r.status_code == 200
    assert r.json()["intent"]["id"] == "unclear" and "모든 모델" in r.json()["intent"]["error"]
    tried = [m for s, m in provider.calls if s == "intent"]  # 모델마다 두 번씩, 사슬을 한 바퀴만
    assert tried == ["qwen/qwen3.8-27b"] * 2 + ["openai/gpt-oss-20b"] * 2 + ["openai/gpt-oss-120b"] * 2


def test_daily_limit_skips_model_next_time(client, provider):
    def intent(b):
        if b["model"] == "qwen/qwen3.8-27b":
            return httpx.Response(429, text="Rate limit reached: tokens per day (TPD)")
        return ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))

    provider.answers["intent"] = intent
    provider.answers["guard"] = lambda b: ok("no")
    first = client.post("/v1/judge", json=judge_body("hi")).json()
    used = next(u for u in first["usage"] if u["step"] == "intent")
    assert (used["model"], used["fallback"]) == ("openai/gpt-oss-20b", True)  # 사용량에 실제로 쓴 모델이 남음
    provider.calls.clear()
    client.post("/v1/judge", json=judge_body("hi again"))
    assert ("intent", "qwen/qwen3.8-27b") not in provider.calls  # 하루 한도에 걸린 모델은 건너뜀
    client.post("/v1/guard", json={"npc": NPC, "text": "hi"})
    assert ("guard", "openai/gpt-oss-20b") in provider.calls  # 같은 모델을 쓰는 무례 판정(/v1/guard)도 바로 대체


def test_strict_json_failure_retries_with_json_object(client, provider):
    def intent(b):
        if b["response_format"]["type"] == "json_schema":
            return httpx.Response(400, text='{"error": {"message": "Failed to validate JSON"}}')
        return ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))

    provider.answers["intent"] = intent
    r = client.post("/v1/judge", json=judge_body("hi")).json()
    assert r["intent"]["error"] is None


# ---------------------------------------------------------------------
# NPC 대답 스트림
# ---------------------------------------------------------------------
REPLY = {
    "npc": NPC,
    "history": [{"who": "npc", "text": "Hi!"}],
    "text": "라떼 하나요",
    "instruction": "Confirm the order. The total is $5.",
}


def test_reply_stream_events_in_order(provider):
    provider.answers["npc"] = lambda b: stream([' "Got', " it, one latte for", " you. That's $5.", " Anything else?", "\nCustomer: no"])
    tts = FakeTts()
    with make_client(provider, tts) as c:
        r = c.post("/v1/npc/reply", json=REPLY)
        assert r.headers["content-type"].startswith("text/event-stream")
        ev = events(r)
        names = [n for n, _ in ev]
        assert names[-1] == "done" and "error" not in names
        text = "".join(d["text"] for n, d in ev if n == "delta")
        assert text == "Got it, one latte for you. That's $5. Anything else?"  # 줄바꿈 뒤(다른 사람 대사)는 버림
        audio = [d for n, d in ev if n == "audio"]
        assert [a["seq"] for a in audio] == [0, 1, 2]
        assert [a["text"] for a in audio] == ["Got it, one latte for you.", "That's $5.", "Anything else?"]
        assert all(a["tts"] for a in audio)
        done = ev[-1][1]
        assert done["text"] == text and done["usage"][0]["step"] == "npc" and done["usage"][0]["output_tokens"] == 20
        assert c.get(f"/v1/audio/{audio[0]['audio_id']}").content == b"RIFF" + audio[0]["text"].encode()
        assert c.get("/v1/audio/nope").status_code == 404


@pytest.mark.parametrize("stream_chunks", [True, False])
def test_reply_replaces_semicolons_in_subtitles_and_speech(provider, stream_chunks):
    provider.answers["npc"] = lambda b: stream(["I'm out with Pepper", ";", " ", "[[curious:0.4]]", "how are you?"])
    tts = FakeTts(wav=True)
    with make_client(provider, tts) as client:
        reply = events(client.post("/v1/npc/reply", json={**REPLY, "stream_chunks": stream_chunks}))

    expected = "I'm out with Pepper. How are you?"
    subtitles = [data["text"] for name, data in reply if name == "delta"]
    audio_phrases = [data["text"] for name, data in reply if name == "audio"]
    assert reply[-1][0] == "done"
    assert reply[-1][1]["text"] == expected
    assert tts.texts == ["I'm out with Pepper.", "How are you?"]
    assert [phrase["text"] for phrase in reply[-1][1]["speech_phrases"]] == tts.texts
    assert "".join(subtitles) == (expected if stream_chunks else "")
    assert audio_phrases == (tts.texts if stream_chunks else [])


def test_slow_tts_chunk_falls_back(provider):
    provider.answers["npc"] = lambda b: stream(["Sure. ", "One latte."])
    with make_client(provider, FakeTts(delay=0.3), tts_chunk_budget_s=0.1) as c:
        audio = [d for n, d in events(c.post("/v1/npc/reply", json=REPLY)) if n == "audio"]
        assert audio and not any(a["tts"] for a in audio) and audio[0]["audio_id"] is None


def test_reply_without_tts_still_marks_chunks(client, provider):
    provider.answers["npc"] = lambda b: stream(["Sure, no problem."])
    ev = events(client.post("/v1/npc/reply", json={**REPLY, "tts": True}))  # 합성 모델이 없으면 tts: false
    assert [d["tts"] for n, d in ev if n == "audio"] == [False]


def test_empty_reply_gets_fallback_line(client, provider):
    provider.answers["npc"] = lambda b: stream(["  ", '"'])
    ev = events(client.post("/v1/npc/reply", json=REPLY))
    assert ev[-1] == ("done", {**ev[-1][1], "text": "Sorry, could you say that again?"})


def test_reply_provider_failure_sends_error_event(client, provider):
    provider.answers["npc"] = lambda b: httpx.Response(500, text="boom")
    ev = events(client.post("/v1/npc/reply", json=REPLY))
    assert ev[-1][0] == "error" and ev[-1][1]["code"] == "provider_unavailable"


def test_slow_first_token_sends_timeout_error(provider):
    async def slow(b):
        await asyncio.sleep(0.5)
        return stream(["Sure."])

    provider.answers["npc"] = slow
    with make_client(provider, reply_first_budget_s=0.2) as c:
        ev = events(c.post("/v1/npc/reply", json=REPLY))
    assert ev[-1][0] == "error" and ev[-1][1]["code"] == "timeout"
    assert not [d for n, d in ev if n == "delta"]


def test_memory_note_reaches_npc_prompt(client, provider):
    seen = {}

    def npc(b):
        seen["system"] = b["messages"][0]["content"]
        return stream(["Hi again, Minsu!"])

    provider.answers["npc"] = npc
    client.post(
        "/v1/npc/reply",
        json={
            **REPLY,
            "memory": {
                "visits": 2,
                "last_order": "a latte",
                "card": {"name": "Minsu", "attitude": "wary", "claims": [], "events": [], "struggled": []},
            },
        },
    )
    assert "Their name: Minsu" in seen["system"] and "wary" in seen["system"] and "Last time they ordered a latte" in seen["system"]


def test_splitter_matches_prototype_screen():
    sp = Splitter()
    assert sp.feed("Sure, ") == []  # 12자 미만이면 쉼표에서 안 끊음
    assert sp.feed("one latte for you, ") == ["Sure, one latte for you,"]
    assert sp.feed("it's $5.50 today. Th") == ["it's $5.50 today."]  # 소수점은 문장 끝이 아님
    assert sp.feed("anks, ") == []  # 쉼표 끊기는 첫 조각만
    assert sp.flush() == ["Thanks,"]


# ---------------------------------------------------------------------
# 힌트·대화 종료·음성
# ---------------------------------------------------------------------
def test_hint(client, provider):
    provider.answers["hint"] = lambda b: ok(
        json.dumps({"say_this": [{"en": "a", "ko": "가"}] * 3, "keywords": [{"en": "k", "ko": "케"}] * 5})
    )
    r = client.post("/v1/hint", json={"npc": NPC, "goal": "order a drink", "history": [{"who": "npc", "text": "Hi!"}]}).json()
    assert len(r["say_this"]) == 2 and len(r["keywords"]) == 3 and r["usage"][0]["step"] == "hint"


def test_hint_failure_is_503(client, provider):
    provider.answers["hint"] = lambda b: httpx.Response(503, text="down")
    r = client.post("/v1/hint", json={"npc": NPC, "goal": "order a drink"})
    assert r.status_code == 503 and r.json()["error"]["code"] == "provider_unavailable"


def test_memory_failure_is_503(client, provider):
    """기억 카드 하나만 만드는 요청이라 공급자 실패는 503 (Spring은 이전 카드를 그대로 둠)."""
    provider.answers["memory"] = lambda b: httpx.Response(503, text="down")
    body = {
        "npc": NPC,
        "outcome": "주문 성공",
        "history": [{"who": "npc", "text": "Hi!"}, {"who": "me", "text": "라떼", "english": "A latte.", "rude": False}],
    }
    r = client.post("/v1/memory", json=body)
    assert r.status_code == 503 and r.json()["error"]["code"] == "provider_unavailable"


def test_memory_card_is_cleaned(client, provider):
    provider.answers["memory"] = lambda b: ok(
        json.dumps(
            {
                "name": "Minsu",
                "attitude": "angry",
                "events": [],
                "claims": ["phone is 010-1234-5678", "says they are a police officer"] + ["x"] * 9,
                "struggled": [],
            }
        )
    )
    body = {"npc": NPC, "outcome": "o", "history": [{"who": "me", "text": "hi"}]}
    r = client.post("/v1/memory", json=body).json()
    card = r["memory_card"]
    assert card["attitude"] == "neutral" and card["claims"][0] == "says they are a police officer" and len(card["claims"]) == 5
    assert [u["step"] for u in r["usage"]] == ["memory"]


def test_tts_unavailable(client):
    r = client.post("/v1/tts", json={"text": "hi"})
    assert r.status_code == 503 and r.json()["error"]["code"] == "tts_unavailable"
