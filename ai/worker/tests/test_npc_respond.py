"""NPC 추론 API (POST /internal/ai/npc/respond, 계약 v2): 요청 검사 → 새 인격 프롬프트 → 대사 반환.

요청은 계약 예시(docs/api/npc-ai-contracts/npc_inference_examples_v2.json)를 그대로 쓴다. 가짜 공급자라 모델을 부르지 않는다.
"""

import asyncio
import copy
import json
from pathlib import Path

import httpx
import pytest

from app import npc_prompt, npc_respond
from conftest import make_client, ok

RESPOND = "/internal/ai/npc/respond"
EXAMPLES = json.loads(
    (Path(__file__).resolve().parents[3] / "docs/api/npc-ai-contracts/npc_inference_examples_v2.json").read_text(encoding="utf-8")
)


def example_request(name: str) -> dict:
    return copy.deepcopy(EXAMPLES["requests"][name])


def answer_with(provider, text: str, end_signal: str = "NONE") -> list[dict]:
    """가짜 공급자가 대사 text와 종료 신호를 모델 출력 JSON으로 돌려주게 하고, 받은 요청 본문을 모아 돌려준다."""
    sent = []

    def answer(body):
        sent.append(body)
        return ok(json.dumps({"replyText": text, "endSignal": end_signal}))

    provider.answers["npc"] = answer
    return sent


def test_opening_returns_the_model_line_with_the_request_ids_and_continue(client, provider):
    sent = answer_with(provider, "Hi! What can I get for you?")
    request = example_request("opening")

    r = client.post(RESPOND, json=request)

    assert r.status_code == 200
    assert r.json() == {
        "requestId": request["requestId"],
        "sessionId": request["sessionId"],
        "npcId": request["npcId"],
        "replyText": "Hi! What can I get for you?",
        "conversationControl": {"action": "CONTINUE", "reason": None},
    }
    messages = sent[0]["messages"]
    assert [m["role"] for m in messages] == ["system"]  # 첫 대사는 가짜 사용자 메시지 없이 (계약 4.1)
    assert messages[0]["content"].startswith("## Rules")
    assert messages[0]["content"].endswith(npc_prompt.REPLY_FORMAT)  # 대사와 종료 신호를 JSON으로 달라는 안내
    assert sent[0]["response_format"]["json_schema"]["schema"] == npc_respond.REPLY_SCHEMA  # 형식 강제


def test_followup_sends_the_history_and_the_user_line_last(client, provider):
    sent = answer_with(provider, "Sure! Small or large?")
    request = example_request("followup")

    r = client.post(RESPOND, json=request)

    assert r.status_code == 200
    expected = [{"role": "assistant" if m["speaker"] == "NPC" else "user", "content": m["text"]} for m in request["conversationHistory"]]
    assert sent[0]["messages"][1:] == [*expected, {"role": "user", "content": request["userText"]}]


@pytest.mark.parametrize("reason", ["USER_END_INTENT", "NATURAL_WRAP_UP"])
def test_followup_turns_the_model_end_signal_into_an_offer_to_end(client, provider, reason):
    answer_with(provider, "See you! Have a great day.", reason)

    r = client.post(RESPOND, json=example_request("followup"))

    assert r.status_code == 200
    assert r.json()["replyText"] == "See you! Have a great day."
    assert r.json()["conversationControl"] == {"action": "OFFER_END", "reason": reason}


def test_an_opening_line_always_continues_even_if_the_model_signals_an_end(client, provider):
    answer_with(provider, "Hi there! What can I get for you?", "NATURAL_WRAP_UP")

    r = client.post(RESPOND, json=example_request("opening"))

    assert r.status_code == 200
    assert r.json()["conversationControl"] == {"action": "CONTINUE", "reason": None}  # 첫 대사는 항상 CONTINUE (계약 7절)


def opening_with_user_text(request: dict):
    request["userText"] = "hi"


def followup_ending_with_user(request: dict):
    request["conversationHistory"].append({"speaker": "USER", "text": "and a cookie"})


def unknown_top_level_key(request: dict):
    request["mood"] = "happy"


def unknown_level(request: dict):
    request["userLevel"] = "Z9"


def profile_without_name(request: dict):
    del request["npcProfile"]["name"]


@pytest.mark.parametrize(
    ("name", "break_request"),
    [
        ("opening", opening_with_user_text),  # 첫 대사는 userText가 null이어야 함 (계약 4.1)
        ("followup", followup_ending_with_user),  # 후속은 마지막 이력 화자가 NPC여야 함 (계약 6)
        ("opening", unknown_top_level_key),
        ("opening", unknown_level),
        ("opening", profile_without_name),
    ],
)
def test_contract_violations_are_rejected_without_calling_the_model(client, provider, name, break_request):
    sent = answer_with(provider, "should not be used")
    request = example_request(name)
    break_request(request)

    r = client.post(RESPOND, json=request)

    assert r.status_code == 422
    body = r.json()
    assert body["requestId"] == request["requestId"]
    assert body["error"]["code"] == "INVALID_REQUEST"
    assert body["error"]["retryable"] is False
    assert sent == []


def test_a_body_that_is_not_json_is_rejected_with_no_request_id(client, provider):
    sent = answer_with(provider, "should not be used")

    r = client.post(RESPOND, content=b"{not json", headers={"Content-Type": "application/json"})

    assert r.status_code == 400
    assert r.json() == {"requestId": None, "error": {"code": "INVALID_JSON", "message": r.json()["error"]["message"], "retryable": False}}
    assert sent == []


def test_a_blank_model_line_is_not_sent_as_a_reply(client, provider):
    answer_with(provider, "   ")
    request = example_request("opening")

    r = client.post(RESPOND, json=request)

    assert r.status_code == 502
    body = r.json()
    assert body["requestId"] == request["requestId"]
    assert (body["error"]["code"], body["error"]["retryable"]) == ("INVALID_AI_OUTPUT", True)


@pytest.mark.parametrize(
    "model_output",
    [
        "Hi there!",  # JSON이 아님
        json.dumps({"replyText": "Hi there!", "endSignal": "MAYBE"}),  # 정해지지 않은 종료 신호
        json.dumps({"replyText": "Hi there!"}),  # 종료 신호 칸 없음
    ],
)
def test_model_output_outside_the_reply_format_is_not_sent(client, provider, model_output):
    provider.answers["npc"] = lambda body: ok(model_output)

    r = client.post(RESPOND, json=example_request("followup"))

    assert r.status_code == 502
    assert (r.json()["error"]["code"], r.json()["error"]["retryable"]) == ("INVALID_AI_OUTPUT", True)


@pytest.mark.parametrize(
    ("provider_status", "http", "code", "retryable"),
    [
        (429, 429, "AI_RATE_LIMITED", True),  # 처리량 한도: 잠시 뒤 다시
        (503, 502, "AI_PROVIDER_ERROR", True),  # 공급자 일시 장애 (재시도·대체 모델까지 실패)
        (401, 502, "AI_PROVIDER_ERROR", False),  # 공급자 키·계정 문제: 다시 보내도 같다 (계약 9절)
    ],
)
def test_provider_failures_come_back_in_the_contract_error_form(client, provider, provider_status, http, code, retryable):
    provider.answers["npc"] = lambda body: httpx.Response(provider_status, json={"error": {"message": "fake"}})
    request = example_request("followup")

    r = client.post(RESPOND, json=request)

    assert r.status_code == http
    body = r.json()
    assert body["requestId"] == request["requestId"]
    assert (body["error"]["code"], body["error"]["retryable"]) == (code, retryable)


def test_a_reply_slower_than_the_processing_limit_is_a_timeout(provider):
    async def slow_answer(body):
        await asyncio.sleep(0.5)
        return ok(json.dumps({"replyText": "Too late.", "endSignal": "NONE"}))

    provider.answers["npc"] = slow_answer
    request = example_request("followup")

    with make_client(provider, respond_budget_s=0.1) as client:  # 계약의 AI 처리 제한(기본 30초)을 테스트에서 줄임
        r = client.post(RESPOND, json=request)

    assert r.status_code == 504
    body = r.json()
    assert body["requestId"] == request["requestId"]
    assert (body["error"]["code"], body["error"]["retryable"]) == ("AI_TIMEOUT", True)


def test_an_unexpected_error_still_answers_in_the_contract_form(client, provider, monkeypatch):
    def broken_prompt(request):
        raise RuntimeError("bug")

    monkeypatch.setattr(npc_respond, "npc_messages", broken_prompt)  # 프로그램 오류를 흉내 냄
    request = example_request("opening")

    r = client.post(RESPOND, json=request)

    assert r.status_code == 500
    body = r.json()
    assert body["requestId"] == request["requestId"]
    assert (body["error"]["code"], body["error"]["retryable"]) == ("INTERNAL_ERROR", False)
    assert "bug" not in body["error"]["message"]  # 내부 원인은 로그에만
