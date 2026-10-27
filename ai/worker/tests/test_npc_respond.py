"""NPC 추론 API (POST /internal/ai/npc/respond, 계약 v2): 요청 검사 → 새 인격 프롬프트 → 대사 반환.

요청은 계약 예시(docs/api/npc-ai-contracts/npc_inference_examples_v2.json)를 그대로 쓴다. 가짜 공급자라 모델을 부르지 않는다.
"""

import copy
import json
from pathlib import Path

import pytest

from conftest import ok

RESPOND = "/internal/ai/npc/respond"
EXAMPLES = json.loads(
    (Path(__file__).resolve().parents[3] / "docs/api/npc-ai-contracts/npc_inference_examples_v2.json").read_text(encoding="utf-8")
)


def example_request(name: str) -> dict:
    return copy.deepcopy(EXAMPLES["requests"][name])


def answer_with(provider, text: str) -> list[dict]:
    """가짜 공급자가 NPC 대사로 text를 돌려주게 하고, 받은 요청 본문을 모아 돌려준다."""
    sent = []

    def answer(body):
        sent.append(body)
        return ok(text)

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


def test_followup_sends_the_history_and_the_user_line_last(client, provider):
    sent = answer_with(provider, "Sure! Small or large?")
    request = example_request("followup")

    r = client.post(RESPOND, json=request)

    assert r.status_code == 200
    expected = [{"role": "assistant" if m["speaker"] == "NPC" else "user", "content": m["text"]} for m in request["conversationHistory"]]
    assert sent[0]["messages"][1:] == [*expected, {"role": "user", "content": request["userText"]}]


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
