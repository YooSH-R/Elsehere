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
    with quiet_client(provider) as c:
        r = c.post("/v1/judge", json=judge_body())
    assert r.status_code == 200
    assert r.json()["intent"]["id"] == "unclear" and r.json()["intent"]["error"]


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


@pytest.mark.parametrize("out", ["[]", "not json"])
def test_memory_card_that_is_not_an_object_is_invalid_output(provider, out):
    provider.answers["memory"] = lambda b: ok(out)
    with quiet_client(provider) as c:
        r = c.post("/v1/memory", json={"npc": NPC, "outcome": "o", "history": [{"who": "me", "text": "hi"}]})
    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


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


# ---------------------------------------------------------------------
# 공급자가 200에 예상과 다른 본문을 줄 때 (코드 리뷰 #3·#4)
# ---------------------------------------------------------------------
@pytest.mark.parametrize("bad", [{"error": {"message": "x"}}, {"choices": []}, {"choices": [{"message": None}]}])
def test_provider_200_with_unexpected_body_is_partial_failure(provider, bad):
    """판정은 실패해도 200. 공급자 탓이므로 의도는 unclear + error, 무례(/v1/guard)는 null + provider_unavailable."""
    provider.answers["intent"] = lambda b: httpx.Response(200, json=bad)
    provider.answers["guard"] = lambda b: httpx.Response(200, json=bad)
    with quiet_client(provider) as c:
        r = c.post("/v1/judge", json=judge_body())
        g = c.post("/v1/guard", json={"npc": judge_body()["npc"], "text": "hello"})
    assert r.status_code == 200
    assert r.json()["intent"]["id"] == "unclear" and r.json()["intent"]["error"]
    assert g.status_code == 200
    assert g.json()["guard"]["abusive"] is None and g.json()["guard"]["error_code"] == "provider_unavailable"


def test_provider_200_that_is_not_json_is_503(provider):
    provider.answers["hint"] = lambda b: httpx.Response(200, text="<html>oops</html>")
    with quiet_client(provider) as c:
        r = c.post("/v1/hint", json={"npc": NPC, "goal": "order a drink"})
    assert r.status_code == 503 and r.json()["error"]["code"] == "provider_unavailable"


def test_stt_200_that_is_not_json_is_503(provider):
    provider.answers["stt"] = lambda b: httpx.Response(200, text="oops")
    with quiet_client(provider) as c:
        r = c.post("/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")})
    assert r.status_code == 503 and r.json()["error"]["code"] == "provider_unavailable"


def test_broken_stream_line_ends_with_error_event(provider):
    """스트림 data 줄이 JSON이 아니면 스트림이 끊기지 않고 error 이벤트로 끝난다."""
    body = 'data: {"choices": [{"delta": {"content": "Hi "}}]}\n\ndata: {broken\n\ndata: [DONE]\n\n'
    provider.answers["npc"] = lambda b: httpx.Response(200, content=body.encode())
    with quiet_client(provider) as c:
        r = c.post("/v1/npc/reply", json={"npc": NPC, "history": [], "text": "hi", "tts": False})
    names = [line.split(": ", 1)[1] for line in r.text.splitlines() if line.startswith("event: ")]
    assert names[-1] == "error"
    assert '"code": "provider_unavailable"' in r.text
