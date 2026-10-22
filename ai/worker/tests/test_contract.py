"""계약 문서(docs/api/ai-worker.md)의 실패 약속이 모델 출력이 깨졌을 때도 지켜지는지. 가짜 공급자라 진짜 호출 없음."""

import httpx
import pytest
from fastapi.testclient import TestClient

from app import services
from app.config import PROFILES, Settings, profile_models
from app.main import create_app
from conftest import TOKEN, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista.", "facts": "", "scene": "Cafe."}
STATE = {"id": "ordering", "desc": "waiting", "intents": [{"id": "order_items", "desc": "orders"}, {"id": "unclear", "desc": "?"}]}


def judge_body(text="one latte please"):
    return {"npc": NPC, "state": STATE, "history": [], "text": text}


def quiet_client(provider) -> TestClient:
    """서버 오류를 테스트 예외로 올리지 않고 응답으로 받는 클라이언트 (Spring이 실제로 받는 것)."""
    s = Settings(
        token=TOKEN,
        base_url="http://fake/v1",
        api_key="x",
        profile="groq",
        models=profile_models("groq")[0],
        role_provider={},
        fallback=dict(PROFILES["profiles"]["groq"]["fallback"]),
        call_timeout_s=1.0,
        retry_pause_s=0.0,
    )
    app = create_app(s, httpx.MockTransport(provider.handler), None, None)
    return TestClient(app, headers={"X-Internal-Token": TOKEN}, raise_server_exceptions=False)


@pytest.mark.parametrize("out", ["[]", '"order_items"', "123", "null"])
def test_intent_output_that_is_not_an_object_is_unclear(provider, out):
    """의도 판정 출력이 JSON 객체가 아니면 판정 전체가 아니라 의도 칸만 실패 (200, unclear + error)."""
    provider.answers["intent"] = lambda b: ok(out)
    provider.answers["guard"] = lambda b: ok("no")
    with quiet_client(provider) as c:
        r = c.post("/v1/judge", json=judge_body())
    assert r.status_code == 200
    assert r.json()["intent"]["id"] == "unclear" and r.json()["intent"]["error"]
    assert r.json()["guard"]["abusive"] is False


@pytest.mark.parametrize("text", ["", "   "])
def test_blank_text_is_invalid_request(client, text):
    """빈 문장은 행동 묘사 안내(dropped)가 아니라 요청 오류."""
    r = client.post("/v1/judge", json=judge_body(text))
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"


@pytest.mark.parametrize("out", ["not json", '{"say_this": []}', "[]"])
def test_hint_output_that_breaks_format_is_invalid_output(provider, out):
    """힌트 출력이 정해진 형식이 아니면 502 invalid_output (Spring의 AI_RESPONSE_INVALID)."""
    provider.answers["hint"] = lambda b: ok(out)
    with quiet_client(provider) as c:
        r = c.post("/v1/hint", json={"npc": NPC, "goal": "order a drink"})
    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


def test_memory_card_that_is_not_an_object_is_partial_failure(provider):
    provider.answers["feedback"] = lambda b: ok('{"good": "g", "improve": {"line": 1, "better": "b", "why": "w"}, "lines": []}')
    provider.answers["memory"] = lambda b: ok("[]")
    with quiet_client(provider) as c:
        r = c.post(
            "/v1/conversation/end",
            json={"npc": NPC, "scenario_title": "t", "goal_reached": True, "outcome": "o", "history": [{"who": "me", "text": "hi"}]},
        )
    assert r.status_code == 200
    assert r.json()["memory_card"] is None and r.json()["errors"]["memory"]


def test_unexpected_error_uses_error_format(provider, monkeypatch):
    """프로그램 오류도 {"error": {"code", "message"}} 형식 (원인은 로그에만)."""

    async def broken(*a, **kw):
        raise RuntimeError("bug")

    monkeypatch.setattr(services, "hint", broken)
    with quiet_client(provider) as c:
        r = c.post("/v1/hint", json={"npc": NPC, "goal": "order a drink"})
    assert r.status_code == 500 and r.json() == {"error": {"code": "internal_error", "message": "AI 워커 내부 오류"}}


def test_unknown_path_uses_error_format(client):
    r = client.get("/v1/nope")
    assert r.status_code == 404 and r.json()["error"]["code"] == "not_found"


def test_wrong_method_uses_error_format(client):
    r = client.get("/v1/judge")
    assert r.status_code == 405 and r.json()["error"]["code"] == "method_not_allowed"


def test_action_only_text_is_still_dropped(client, provider):
    r = client.post("/v1/judge", json=judge_body("*waves*")).json()
    assert r["dropped"] is True and provider.calls == []
