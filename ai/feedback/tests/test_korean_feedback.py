"""한국어 원문을 보존하면서 해당 의미의 영어 표현을 제공한다. 실제 모델 호출 없음."""

import json
from pathlib import Path

import pytest

from feedback.clients import CallableClient
from feedback.prompt import build_messages
from feedback.service import generate_feedback

ROOT = Path(__file__).resolve().parents[1]


def korean_case():
    request = json.loads((ROOT / "samples/inputs/free_grammar.json").read_text(encoding="utf-8"))
    body = json.loads((ROOT / "samples/fixtures/free_grammar.llm.json").read_text(encoding="utf-8"))
    original = request["turns"][1]["final_text"]
    korean = "역에 가고 싶어요."
    request["turns"][1]["final_text"] = korean
    body = json.loads(json.dumps(body, ensure_ascii=False).replace(original, korean))
    review = body["result"]["turn_reviews"][0]
    review["suggestions"][0]["kind"] = "context_fit"
    review["explanation_ko"] = "가고 싶은 장소를 영어로 말할 때 I want to go to를 써요."
    return request, body


def run(request, body):
    return generate_feedback(request, CallableClient(lambda _: json.dumps(body, ensure_ascii=False)), ai_allowed=True)


def test_korean_turn_is_explicitly_requested_as_an_english_expression():
    request, _ = korean_case()
    messages = build_messages(request)
    assert json.loads(messages[1]["content"])["english_expression_turn_ids"] == ["t02"]
    assert "한국어로 의도를 전달한 발화에는 영어 표현" in messages[0]["content"]


def test_verdict_rules_give_every_clear_korean_turn_an_english_expression_instead_of_not_assessable():
    request, _ = korean_case()
    system = build_messages(request)[0]["content"]
    rules = system.split("## 발화별 판정")[1].split("\n## ")[0]
    # 한국어만 쓴 발화도 뜻이 분명하면 영어 표현을 받고, 뜻이 불분명할 때만 되묻는다.
    assert "한국어만의 발화는 not_assessable" not in rules
    assert "한국어만 있어도 의도가 분명하면 improvement_suggested" in rules
    assert "의도가 불분명할 때만 needs_clarification" in rules


def test_korean_turn_keeps_original_and_returns_english_context_expression():
    request, body = korean_case()
    response = run(request, body)
    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    assert review["original_text"] == "역에 가고 싶어요."
    assert review["suggestions"][0]["kind"] == "context_fit"
    assert review["suggestions"][0]["suggested_expression"] == "I want to go to the station."


@pytest.mark.parametrize("english", [None, "역에 가고 싶어요."])
def test_clear_korean_turn_cannot_silently_complete_without_an_english_expression(english):
    request, body = korean_case()
    result = body["result"]
    review = result["turn_reviews"][0]
    result["vocab_candidates"] = []
    if english is None:
        review.update(verdict="appropriate", suggestions=[])
        result["summary_suggestion_id"] = None
    else:
        review["suggestions"][0]["suggested_expression"] = english
    assert run(request, body)["problem"]["code"] == "INVALID_MODEL_OUTPUT"


def test_unclear_korean_turn_does_not_invent_an_english_answer():
    request, body = korean_case()
    review = body["result"]["turn_reviews"][0]
    review.update(verdict="needs_clarification", suggestions=[])
    body["result"].update(summary_suggestion_id=None, vocab_candidates=[])
    response = run(request, body)
    assert response["status"] == "completed"
    assert response["result"]["turn_reviews"][0]["suggestions"] == []
