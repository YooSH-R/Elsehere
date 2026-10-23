"""무례 판정 /v1/guard 테스트. 판정 모델은 가짜 공급자가 yes/no로 답한다."""

import asyncio

import httpx

from app import config
from app.config import Settings
from app.guard import guard_result
from conftest import TOKEN, make_client, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista at Maple Cafe.", "facts": "Menu: latte $5.", "scene": "Inside Maple Cafe."}
HISTORY = [{"who": "npc", "text": "Hi! What can I get for you?"}]


def guard_body(text, history=HISTORY):
    return {"npc": NPC, "history": history, "text": text}


def test_rude_line_is_judged_by_guard_model_only(client, provider):
    provider.answers["guard"] = lambda b: ok("yes")
    r = client.post("/v1/guard", json=guard_body("you idiot, hurry up"))
    assert r.status_code == 200
    body = r.json()
    assert body["text"] == "you idiot, hurry up" and body["dropped"] is False
    assert body["guard"] == {"abusive": True, "error": None, "error_code": None}
    assert [u["step"] for u in body["usage"]] == ["guard"]
    assert [step for step, _ in provider.calls] == ["guard"]  # 의도·번역은 부르지 않는다


def test_action_only_line_is_dropped_without_model_call(client, provider):
    r = client.post("/v1/guard", json=guard_body("*총을 꺼낸다*")).json()
    assert r == {"text": "", "dropped": True, "guard": None, "usage": []}
    assert provider.calls == []


def test_blank_text_is_invalid_request(client, provider):
    r = client.post("/v1/guard", json=guard_body("   "))
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"
    assert provider.calls == []


def test_slow_guard_hits_guard_budget(provider):
    """판정이 GUARD_BUDGET_S를 넘기면 미판정(timeout)으로 200. 의도·번역의 상한(JUDGE_BUDGET_S)과는 따로다."""

    async def slow(b):
        await asyncio.sleep(0.5)
        return ok("yes")

    provider.answers["guard"] = slow
    with make_client(provider, guard_budget_s=0.2, judge_budget_s=5.0) as c:
        r = c.post("/v1/guard", json=guard_body("hello"))
    assert r.status_code == 200
    assert r.json()["guard"]["abusive"] is None and r.json()["guard"]["error_code"] == "timeout"


def test_guard_budget_from_env(monkeypatch):
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q"}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    assert Settings.from_env().guard_budget_s == 5.0
    env["GUARD_BUDGET_S"] = "2.5"
    assert Settings.from_env().guard_budget_s == 2.5


def test_unclear_answer_is_not_counted_as_abusive(client, provider):
    """빈 답·거절은 미판정 (예전에는 무례로 셌다). Spring은 무례로 세지 않고 번역 카드도 보여 주지 않는다."""
    provider.answers["guard"] = lambda b: ok("")
    r = client.post("/v1/guard", json=guard_body("커피 줘")).json()
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "empty"


def test_provider_failure_is_unjudged(client, provider):
    provider.answers["guard"] = lambda b: httpx.Response(503, text="down")
    r = client.post("/v1/guard", json=guard_body("hello"))
    assert r.status_code == 200
    assert r.json()["guard"]["abusive"] is None and r.json()["guard"]["error_code"] == "provider_unavailable"


def test_guard_reads_only_clear_yes_no():
    assert guard_result("Yes.")["abusive"] is True and guard_result(" no, it's polite")["abusive"] is False
    assert guard_result("") == {"abusive": None, "error": "판정 모델이 빈 답을 냄", "error_code": "empty"}
    for out in ("I can't help with that.", "nothing rude", "yesterday", "Maybe"):  # 거절·다른 말은 무례도 정상도 아님
        assert guard_result(out)["abusive"] is None and guard_result(out)["error_code"] == "unparsed", out
