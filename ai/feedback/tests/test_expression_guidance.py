"""표현 확장은 정답 교정과 구분하고 확정 사용자 원문에 연결한다."""

import copy
import json
from pathlib import Path

import pytest

from feedback.clients import FixtureClient
from feedback.service import generate_feedback

ROOT = Path(__file__).resolve().parents[1]


def conversation(text, verdict, examples, suggestion=None):
    request = json.loads((ROOT / "samples/inputs/free_order.json").read_text(encoding="utf-8"))
    body = json.loads((ROOT / "samples/fixtures/free_order.llm.json").read_text(encoding="utf-8"))
    user = next(turn for turn in request["turns"] if turn["speaker"] == "user")
    user.update(final_text=text, assistance_ids=[])
    request["turns"] = request["turns"][: request["turns"].index(user) + 1]
    request["assistance_events"] = []
    evidence = {"turn_id": user["turn_id"], "quote": text}
    review = {
        "review_id": "review1",
        "turn_id": user["turn_id"],
        "original_text": text,
        "verdict": verdict,
        "explanation_ko": "무엇을 요청하는지 상황에 맞는 문장으로 말해 볼 수 있어요.",
        "assisted": False,
        "suggestions": [],
        "expression_examples": examples,
    }
    if suggestion:
        review["suggestions"] = [
            {
                "suggestion_id": "suggestion1",
                "kind": "context_fit",
                "source": evidence,
                "suggested_expression": suggestion,
                "explanation_ko": "원하는 음료와 크기·온도를 한 문장으로 연결해요.",
            }
        ]
    body["result"].update(
        conversation_summary={"text_ko": "요청하는 표현을 연습했어요.", "evidence": [evidence]},
        communication={"judgement": "partially_clear", "explanation_ko": "요청 대상을 언급했어요.", "evidence": [evidence]},
        review_tags=[],
        turn_reviews=[review],
        summary_suggestion_id="suggestion1" if suggestion else None,
        vocab_candidates=[],
    )
    return request, body


UMBRELLA = [
    {"condition_ko": "빌리고 싶을 때", "expression": "Could I borrow an umbrella, please?", "meaning_ko": "우산을 빌릴 수 있을까요?"},
    {"condition_ko": "파는 곳을 물을 때", "expression": "Where can I buy an umbrella?", "meaning_ko": "우산을 어디에서 살 수 있나요?"},
]


def test_ambiguous_request_keeps_conditional_examples_without_asserting_an_intent():
    request, body = conversation("umbrella please", "needs_clarification", UMBRELLA)

    response = generate_feedback(request, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    assert review["verdict"] == "needs_clarification"
    assert review["suggestions"] == []
    assert review["expression_examples"] == UMBRELLA


def test_understandable_word_list_can_offer_full_requests_beyond_grammar_repair():
    examples = [
        {
            "condition_ko": "원하는 음료를 주문할 때",
            "expression": "I'd like a large cold coffee, please.",
            "meaning_ko": "차가운 커피 큰 것으로 주세요.",
        },
        {
            "condition_ko": "같은 주문을 다른 말로 할 때",
            "expression": "Could I have a large cold coffee?",
            "meaning_ko": "차가운 커피 큰 것으로 받을 수 있을까요?",
        },
        {
            "condition_ko": "메뉴를 선택해 말할 때",
            "expression": "I'll have a large cold coffee, please.",
            "meaning_ko": "차가운 커피 큰 것으로 할게요.",
        },
    ]
    request, body = conversation("Coffee. Large. Cold.", "improvement_suggested", examples, examples[0]["expression"])

    response = generate_feedback(request, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    assert review["original_text"] == "Coffee. Large. Cold."
    assert review["expression_examples"] == examples


@pytest.mark.parametrize("text", ["To go, please.", "For here, please.", "No onions, please.", "A Coke, please."])
def test_natural_short_answers_do_not_require_corrections_or_expansion(text):
    request, body = conversation(text, "appropriate", [])

    response = generate_feedback(request, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    assert review["verdict"] == "appropriate"
    assert review["suggestions"] == []
    assert review["expression_examples"] == []


@pytest.mark.parametrize("examples", [[UMBRELLA[0]], UMBRELLA + UMBRELLA, [UMBRELLA[0], copy.deepcopy(UMBRELLA[0])]])
def test_examples_are_two_or_three_distinct_options(examples):
    request, body = conversation("umbrella please", "needs_clarification", examples)

    response = generate_feedback(request, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "failed"
    assert response["problem"]["code"] == "INVALID_MODEL_OUTPUT"
