"""AI 워커 API 테스트 (모델 호출 없음, 몇 초 안에 끝남).   pytest -q   (ai/worker 에서)"""

import asyncio
import json

import httpx

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
    provider.answers["guard"] = lambda b: ok("no")
    r = client.post("/v1/judge", json=judge_body("two iced lattes and a muffin to go")).json()
    assert r["intent"] == {
        "id": "order_items",
        "ids": ["order_items"],
        "error": None,
        "slots": {"items": [{"name": "latte", "iced": True, "qty": 2}, {"name": "muffin", "iced": False, "qty": 1}], "choice": "to_go"},
    }
    assert r["guard"] == {"abusive": False, "error": None, "error_code": None}
    assert r["translation"] is None  # 한국어가 없으면 번역을 부르지 않음
    assert {u["step"] for u in r["usage"]} == {"intent", "guard"}
    assert all(u["input_tokens"] == 100 for u in r["usage"])


def test_intent_schema_follows_place_slots(client, provider):
    """약국처럼 아이스·선택지가 없는 장소는 판정 형식에도 그 칸이 없다 (장소 데이터 형식)."""
    seen = {}

    def intent(b):
        seen["schema"] = b["response_format"]["json_schema"]["schema"]
        seen["prompt"] = b["messages"][1]["content"]
        return ok(json.dumps({"intent": "describe_symptom", "items": []}))

    provider.answers["intent"] = intent
    provider.answers["guard"] = lambda b: ok("no")
    r = client.post("/v1/judge", json=judge_body("I have a headache", PHARMACY_STATE)).json()
    assert r["intent"]["id"] == "describe_symptom"
    props = seen["schema"]["properties"]
    assert set(props) == {"intents", "items"}
    assert set(props["items"]["items"]["properties"]) == {"name", "qty"}
    assert "products: pain reliever, cough syrup" in seen["prompt"] and "iced" not in seen["prompt"]


def test_several_actions_in_one_line(client, provider):
    """'to go. cash': 포장 고르기 단계에서도 다음 단계 의도(결제)를 이어서 고를 수 있고, 말한 순서대로 돌려준다."""
    seen = {}

    def intent(b):
        seen["schema"] = b["response_format"]["json_schema"]["schema"]
        return ok(json.dumps({"intents": ["choose_here_or_to_go", "pay", "fly"], "items": [], "choice": "to_go"}))

    provider.answers["intent"] = intent
    provider.answers["guard"] = lambda b: ok("no")
    state = {
        **CAFE_STATE,
        "id": "paying",
        "intents": [{"id": "choose_here_or_to_go", "desc": "here or to go"}, {"id": "unclear", "desc": "?"}],
        "more_intents": [{"id": "pay", "desc": "pays"}],
    }
    r = client.post("/v1/judge", json=judge_body("to go. cash", state)).json()
    assert r["intent"]["ids"] == ["choose_here_or_to_go", "pay"] and r["intent"]["id"] == "choose_here_or_to_go"  # 모르는 의도(fly)는 버림
    assert "pay" in seen["schema"]["properties"]["intents"]["items"]["enum"]


def test_korean_rude_line_hides_translation(client, provider):
    provider.answers["intent"] = lambda b: ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))
    provider.answers["guard"] = lambda b: ok("yes")
    provider.answers["translate"] = lambda b: ok("Shut up and give me coffee.")
    r = client.post("/v1/judge", json=judge_body("닥치고 커피 내놔")).json()
    assert r["guard"]["abusive"] is True
    assert r["translation"] == {"english": "Shut up and give me coffee.", "show": False, "error": None}


def test_unclear_guard_answer_is_not_counted_as_abusive(client, provider):
    """빈 답·거절은 미판정 (예전에는 무례로 셌다). 번역 카드도 보여 주지 않는다."""
    provider.answers["intent"] = lambda b: ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))
    provider.answers["guard"] = lambda b: ok("")
    provider.answers["translate"] = lambda b: ok("Give me coffee.")
    r = client.post("/v1/judge", json=judge_body("커피 줘")).json()
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "empty"
    assert r["translation"]["show"] is False


def test_guard_provider_failure_is_unjudged(client, provider):
    """판정 모델이 대체 모델까지 모두 실패해도 '무례 아님'이 아니라 미판정 + 공급자 오류 코드."""
    provider.answers["intent"] = lambda b: ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))
    provider.answers["guard"] = lambda b: httpx.Response(503, text="down")
    r = client.post("/v1/judge", json=judge_body("hello"))
    assert r.status_code == 200
    assert r.json()["guard"]["abusive"] is None and r.json()["guard"]["error_code"] == "provider_unavailable"


def test_slow_guard_hits_judge_budget(provider):
    """무례 판정이 상한을 넘기면 그 칸만 미판정(timeout). 의도는 정상으로 돌려준다."""

    async def slow(b):
        await asyncio.sleep(0.5)
        return ok("yes")

    provider.answers["intent"] = lambda b: ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))
    provider.answers["guard"] = slow
    with make_client(provider, judge_budget_s=0.2) as c:
        r = c.post("/v1/judge", json=judge_body("hello")).json()
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "timeout"
    assert r["intent"]["id"] == "unclear" and r["intent"]["error"] is None


def test_action_only_is_dropped_without_model_call(client, provider):
    r = client.post("/v1/judge", json=judge_body("*총을 꺼낸다*")).json()
    assert r["dropped"] is True and "행동 묘사" in r["notice"]
    assert provider.calls == []


def test_action_removed_from_mixed_line(client, provider):
    provider.answers["intent"] = lambda b: ok(
        json.dumps({"intent": "order_items", "items": [{"name": "latte", "iced": False, "qty": 1}], "choice": "none"})
    )
    provider.answers["guard"] = lambda b: ok("no")
    r = client.post("/v1/judge", json=judge_body("*waves* one latte please")).json()
    assert r["text"] == "one latte please" and r["notice"] is not None


def test_intent_failure_keeps_turn_going(client, provider):
    """모델이 대체 모델까지 모두 실패해도 판정은 200: 의도는 unclear + error (턴은 이어 감)."""
    provider.answers["intent"] = lambda b: httpx.Response(503, text="down")
    provider.answers["guard"] = lambda b: ok("no")
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
    assert ("guard", "openai/gpt-oss-20b") in provider.calls  # 같은 모델을 쓰는 무례 판정도 바로 대체


def test_strict_json_failure_retries_with_json_object(client, provider):
    def intent(b):
        if b["response_format"]["type"] == "json_schema":
            return httpx.Response(400, text='{"error": {"message": "Failed to validate JSON"}}')
        return ok(json.dumps({"intent": "unclear", "items": [], "choice": "none"}))

    provider.answers["intent"] = intent
    provider.answers["guard"] = lambda b: ok("no")
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
