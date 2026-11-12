"""축약 모델 응답을 기존 공개 계약으로 복원한다. 실제 모델 호출은 하지 않는다."""

import copy
import json
from pathlib import Path

import pytest
from run_evaluation import validation_diagnostic

from feedback.clients import CallableClient, FixtureClient
from feedback.gms import feedback_response_format
from feedback.models import LLM_OUTPUT, validate_pair
from feedback.prompt import build_messages
from feedback.service import generate_feedback

SAMPLES = Path(__file__).resolve().parents[1] / "samples"


def sample(name):
    payload = json.loads((SAMPLES / "inputs" / f"{name}.json").read_text(encoding="utf-8"))
    body = json.loads((SAMPLES / "fixtures" / f"{name}.llm.json").read_text(encoding="utf-8"))
    return payload, body


def compact(body):
    body = copy.deepcopy(body)
    body["format_version"] = "compact-v1"
    result = body.get("result")
    if result is None:
        return body
    reviews = {review["review_id"]: review["turn_id"] for review in result["turn_reviews"]}
    suggestions = {
        suggestion["suggestion_id"]: review["turn_id"] for review in result["turn_reviews"] for suggestion in review["suggestions"]
    }
    if result["summary_suggestion_id"] is not None:
        result["summary_suggestion_id"] = suggestions[result["summary_suggestion_id"]]
    for review in result["turn_reviews"]:
        for field in ("review_id", "original_text", "assisted"):
            review.pop(field)
        for suggestion in review["suggestions"]:
            suggestion.pop("suggestion_id")
    for tag in result["review_tags"]:
        tag.pop("tag_id")
        tag.pop("assisted")
    for candidate in result["vocab_candidates"]:
        candidate.pop("candidate_id")
        source = candidate["source"]
        if source["type"] == "suggestion":
            source["review_id"] = reviews[source["review_id"]]
            source["suggestion_id"] = suggestions[source["suggestion_id"]]
    return body


def test_compact_feedback_restores_original_and_references_without_losing_assessment():
    payload, legacy = sample("free_grammar")
    response = generate_feedback(payload, FixtureClient(compact(legacy)), ai_allowed=True)

    assert response["status"] == "completed"
    result = response["result"]
    review = result["turn_reviews"][0]
    original = legacy["result"]["turn_reviews"][0]
    assert review["original_text"] == original["original_text"]
    assert review["assisted"] == original["assisted"]
    assert review["review_id"] == review["turn_id"]
    assert review["explanation_ko"] == original["explanation_ko"]
    assert review["suggestions"][0]["suggested_expression"] == original["suggestions"][0]["suggested_expression"]
    assert review["suggestions"][0]["source"] == original["suggestions"][0]["source"]
    assert result["summary_suggestion_id"] == review["suggestions"][0]["suggestion_id"] == review["turn_id"]
    assert result["conversation_summary"] == legacy["result"]["conversation_summary"]
    assert "format_version" not in response


def test_speaker_roles_filter_npc_targets_without_changing_the_public_contract():
    payload, legacy = sample("free_grammar")
    body = compact(legacy)
    body["result"]["situation_expressions"] = [
        {
            "expression": "What can I get you?",
            "meaning_ko": "무엇을 드릴까요?",
            "usage_ko": "직원이 주문을 받아요.",
            "example_en": "What can I get you?",
            "example_ko": "무엇을 드릴까요?",
            "speaker_role": "npc",
        },
        {
            "expression": "Have a good one!",
            "meaning_ko": "좋은 하루 보내세요!",
            "usage_ko": "헤어질 때 인사해요.",
            "example_en": "Have a good one!",
            "example_ko": "좋은 하루 보내세요!",
            "speaker_role": "both",
        },
    ]
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)
    assert response["status"] == "completed"
    expressions = response["result"]["situation_expressions"]
    assert [item["expression"] for item in expressions] == ["Have a good one!"]
    assert "speaker_role" not in expressions[0]


@pytest.mark.parametrize(
    ("learner_line", "npc_reply", "expression", "meaning", "usage", "has_fixed_example"),
    [
        (
            "Two croissants, one for me and one for a friend.",
            "Of course. Here are your two croissants.",
            "Could I get two croissants, please?",
            "크루아상 두 개 주세요.",
            "크루아상 두 개를 정중하게 주문할 때 써요.",
            True,
        ),
        (
            "I'm looking for the post office. Is it far?",
            "It's a short walk from here.",
            "How do I get to the post office?",
            "우체국에 어떻게 가나요?",
            "우체국에 가는 길을 물을 때 써요.",
            False,
        ),
    ],
    ids=["ordering", "asking-directions"],
)
def test_situation_examples_use_the_actual_dialogue_at_the_same_bakery(
    learner_line, npc_reply, expression, meaning, usage, has_fixed_example
):
    payload, _ = sample("free_grammar")
    payload["scope"]["npc_id"] = "BAKERY_STAFF"
    payload["context"].update(scene_id="BAKERY", mode="quest", scene_description="빵집 · 간식 고르기")
    for turn, line in zip(payload["turns"], ("Hi there. How can I help?", learner_line, npc_reply), strict=True):
        turn["final_text"] = line
    source = {"turn_id": "t02", "quote": learner_line}

    def model_reply(messages):
        prompt = json.loads(messages[-1]["content"])
        candidate = prompt["situation_expression_candidate"]
        assert (None if candidate is None else candidate["expression"]) == (expression if has_fixed_example else None)
        assert "실제로 나눈 대화의 목적" in messages[0]["content"]
        assert [turn["final_text"] for turn in prompt["turns"]] == [turn["final_text"] for turn in payload["turns"]]
        return json.dumps(
            {
                "outcome": "usable",
                "problem": None,
                "format_version": "compact-v1",
                "result": {
                    "conversation_summary": {"text_ko": "빵집에서 직원과 대화했어요.", "evidence": [source]},
                    "communication": {"judgement": "clear", "explanation_ko": "요청을 전달했어요.", "evidence": [source]},
                    "review_tags": [],
                    "turn_reviews": [
                        {
                            "turn_id": "t02",
                            "verdict": "appropriate",
                            "explanation_ko": "의도가 분명해요.",
                            "suggestions": [],
                        }
                    ],
                    "summary_suggestion_id": None,
                    "vocab_candidates": [],
                    "situation_expressions": [
                        {
                            "expression": expression,
                            "meaning_ko": meaning,
                            "usage_ko": usage,
                            "example_en": expression,
                            "example_ko": meaning,
                            "speaker_role": "learner",
                        }
                    ],
                },
            }
        )

    response = generate_feedback(payload, CallableClient(model_reply), ai_allowed=True)

    assert response["status"] == "completed"
    assert [item["expression"] for item in response["result"]["situation_expressions"]] == [expression]


def test_provider_schema_does_not_ask_model_to_repeat_server_owned_fields():
    payload, _ = sample("free_grammar")
    schema = feedback_response_format(build_messages(payload))["json_schema"]["schema"]

    assert schema["properties"]["format_version"]["enum"] == ["compact-v1"]
    assert "format_version" in schema["required"]
    removed = {
        "TurnReview": {"review_id", "original_text", "assisted"},
        "Suggestion": {"suggestion_id"},
        "ReviewTag": {"tag_id", "assisted"},
        "VocabCandidate": {"candidate_id"},
    }
    for name, fields in removed.items():
        definition = schema["$defs"][name]
        assert not fields.intersection(definition["properties"])
        assert set(definition["required"]) == set(definition["properties"])
        assert definition["additionalProperties"] is False
    assert schema["$defs"]["TurnReview"]["properties"]["turn_id"]["enum"] == ["t02"]


@pytest.mark.parametrize("name", [path.stem for path in sorted((SAMPLES / "inputs").glob("*.json"))])
def test_compact_and_legacy_samples_preserve_all_learning_content(name):
    payload, body = sample(name)
    expected = generate_feedback(payload, FixtureClient(body), ai_allowed=True)
    client = FixtureClient(compact(body))
    actual = generate_feedback(payload, client, ai_allowed=True)

    validate_pair(payload, actual)
    assert actual["status"] == expected["status"]
    assert actual["problem"] == expected["problem"]
    assert compact(actual) == compact(expected)
    users = {turn["turn_id"]: turn for turn in payload["turns"] if turn["speaker"] == "user" and turn["text_status"] == "final"}
    assert client.calls == bool(users)
    if actual["result"]:
        for review in actual["result"]["turn_reviews"]:
            assert review["original_text"] == users[review["turn_id"]]["final_text"]
            assert review["assisted"] == bool(users[review["turn_id"]]["assistance_ids"])


@pytest.mark.parametrize(
    "mutation",
    [
        lambda body: body.update(format_version="unknown"),
        lambda body: body["result"].update(turn_reviews=[]),
        lambda body: body["result"]["turn_reviews"].append(copy.deepcopy(body["result"]["turn_reviews"][0])),
        lambda body: body["result"]["turn_reviews"][0].update(turn_id="missing"),
        lambda body: body["result"]["turn_reviews"][0].update(turn_id="t01"),
        lambda body: body["result"]["turn_reviews"][0].update(turn_id=[]),
        lambda body: body["result"]["turn_reviews"][0].update(suggestions=None),
        lambda body: body["result"]["turn_reviews"][0]["suggestions"][0]["source"].update(quote="invented"),
        lambda body: body["result"]["turn_reviews"][0]["suggestions"][0]["source"].update(turn_id="t01"),
        lambda body: body["result"].update(summary_suggestion_id="missing"),
        lambda body: body["result"]["vocab_candidates"][0]["source"].update(review_id="missing"),
        lambda body: body["result"]["vocab_candidates"][0]["source"].update(suggestion_id="missing"),
        lambda body: body["result"]["review_tags"][0]["evidence"][0].update(turn_id="t01"),
        lambda body: body["result"]["review_tags"][0]["evidence"][0].update(quote="invented"),
    ],
)
def test_compact_output_keeps_invalid_reference_and_evidence_rejection(mutation):
    payload, body = sample("free_grammar")
    body = compact(body)
    mutation(body)
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "failed"
    assert response["problem"]["code"] == "INVALID_MODEL_OUTPUT"
    assert response["result"] is None


@pytest.mark.parametrize(
    "path,field,value",
    [
        ("turn_reviews", "original_text", "replacement"),
        ("turn_reviews", "assisted", False),
        ("turn_reviews", "review_id", "invented"),
        ("review_tags", "tag_id", "invented"),
        ("review_tags", "assisted", True),
        ("vocab_candidates", "candidate_id", "invented"),
    ],
)
def test_compact_output_cannot_override_server_fields(path, field, value):
    payload, body = sample("free_grammar")
    body = compact(body)
    body["result"][path][0][field] = value
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)
    assert response["problem"]["code"] == "INVALID_MODEL_OUTPUT"


def test_compact_output_preserves_review_order_and_does_not_repair_missing_evaluations():
    payload, body = sample("mixed_five_turns")
    for mutation in (lambda rows: rows.reverse(), lambda rows: rows.pop()):
        reduced = compact(body)
        mutation(reduced["result"]["turn_reviews"])
        response = generate_feedback(payload, FixtureClient(reduced), ai_allowed=True)
        assert response["problem"]["code"] == "INVALID_MODEL_OUTPUT"


def test_public_normalized_model_schema_still_requires_original_text_and_assisted():
    properties = LLM_OUTPUT.json_schema()["$defs"]["TurnReview"]["properties"]
    assert {"review_id", "original_text", "assisted"}.issubset(properties)


def test_evaluation_diagnostic_checks_compact_evidence_after_restoration():
    payload, body = sample("free_grammar")
    body = compact(body)
    body["result"]["turn_reviews"][0]["suggestions"][0]["source"]["quote"] = "invented"
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert validation_diagnostic(payload, response, json.dumps(body)) == "인용이 final_text에 없다"


def test_compact_fields_without_version_are_not_silently_repaired():
    payload, body = sample("free_grammar")
    body = compact(body)
    del body["format_version"]
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)
    assert response["problem"]["code"] == "INVALID_MODEL_OUTPUT"


def test_maximum_length_turn_id_is_preserved_in_generated_ids_and_references():
    payload, body = sample("free_grammar")
    body = compact(body)
    turn_id = "u" * 128
    payload = json.loads(json.dumps(payload).replace('"t02"', json.dumps(turn_id)))
    body = json.loads(json.dumps(body).replace('"t02"', json.dumps(turn_id)))
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    assert review["review_id"] == review["suggestions"][0]["suggestion_id"] == turn_id
    assert response["result"]["summary_suggestion_id"] == turn_id


def test_vocab_expression_inside_its_quote_is_kept_with_the_exact_quote():
    # 모델이 원문 일부("go to the station")만 표현으로 뽑고 quote에는 원문 전체를 넣은 경우
    payload, body = sample("free_grammar")
    body = compact(body)
    body["result"]["vocab_candidates"] = [
        {
            "kind": "idiom",
            "expression": "go to the station",
            "meaning_ko": "역에 가다",
            "example_en": "I go to the station every morning.",
            "example_ko": "나는 매일 아침 역에 가요.",
            "source": {"type": "user_turn", "turn_id": "t02", "quote": "I want go to the station."},
        }
    ]
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    candidate = response["result"]["vocab_candidates"][0]
    assert candidate["expression"] == candidate["source"]["quote"] == "go to the station"


def test_vocab_candidate_not_found_in_its_source_is_dropped_without_failing_feedback():
    payload, body = sample("free_grammar")
    body = compact(body)
    body["result"]["vocab_candidates"][0]["expression"] = "I'd like to go to the station."
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    assert response["result"]["vocab_candidates"] == []
    assert response["result"]["turn_reviews"][0]["suggestions"][0]["suggested_expression"] == "I want to go to the station."


def test_voice_surface_only_suggestion_is_dropped_and_review_becomes_appropriate():
    # 음성 원문에 대소문자·물음표만 바꾼 교정은 계약상 오류가 아니다. 그 교정만 빼고 피드백은 살린다
    payload, body = sample("free_grammar")
    turn = next(turn for turn in payload["turns"] if turn["turn_id"] == "t02")
    turn.update(input_type="voice", original_transcript="what is your favorite movie", final_text="what is your favorite movie")
    body = compact(body)
    result = body["result"]
    for evidence in result["conversation_summary"]["evidence"] + result["communication"]["evidence"]:
        if evidence["turn_id"] == "t02":
            evidence["quote"] = "what is your favorite movie"
    for tag in result["review_tags"]:
        for evidence in tag["evidence"]:
            evidence["quote"] = "what is your favorite movie"
    suggestion = result["turn_reviews"][0]["suggestions"][0]
    suggestion["source"]["quote"] = "what is your favorite movie"
    suggestion["suggested_expression"] = "What is your favorite movie?"
    result["vocab_candidates"][0]["expression"] = "What is your favorite movie?"
    result["vocab_candidates"][0]["source"]["quote"] = "What is your favorite movie?"
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    assert review["verdict"] == "appropriate"
    assert review["suggestions"] == []
    assert response["result"]["summary_suggestion_id"] is None
    assert response["result"]["vocab_candidates"] == []


def _grammar_feedback_with_explanation(explanation):
    payload, body = sample("free_grammar")
    body = compact(body)
    review = body["result"]["turn_reviews"][0]
    review["explanation_ko"] = explanation
    review["suggestions"][0]["explanation_ko"] = explanation
    return payload, body


def test_explanation_naming_a_fix_missing_from_the_suggestion_is_rewritten_to_the_actual_change():
    # 설명은 “wanna”를 넣으라는데 추천 문장은 to만 넣었다. 화면의 '이렇게 말해요'와 '이유'가 서로 다르면 안 된다 (S15P31A305-780)
    payload, body = _grammar_feedback_with_explanation("“want go”에는 “wanna”가 빠져 있어 자연스럽지 않아요.")
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    assert response["status"] == "completed"
    review = response["result"]["turn_reviews"][0]
    for explanation in (review["explanation_ko"], review["suggestions"][0]["explanation_ko"]):
        assert "wanna" not in explanation
        assert "“want go”" in explanation and "“want to go”" in explanation


def test_review_explanation_off_the_suggestion_takes_the_suggestions_matching_explanation():
    # 한국어 뜻 풀이와 "want to ~" 같은 형태 설명은 원문과 비교하지 않는다. 맞는 쪽 설명으로 어긋난 쪽을 바꾼다
    fitting = "“역에 가고 싶어요”라는 뜻이면 “want to go”처럼 “want to ~” 형태로 말해요."
    payload, body = _grammar_feedback_with_explanation(fitting)
    body["result"]["turn_reviews"][0]["explanation_ko"] = "“want go”에는 “wanna”가 빠져 있어요."
    response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)

    review = response["result"]["turn_reviews"][0]
    assert review["explanation_ko"] == fitting
    assert review["suggestions"][0]["explanation_ko"] == fitting


def test_model_writes_the_suggestion_before_explaining_it():
    # 설명을 먼저 쓰면 설명과 다른 문장을 고치는 일이 생긴다. 교정 문장을 먼저 정하고 그 교정을 설명하게 한다
    payload, _ = sample("free_grammar")
    schema = feedback_response_format(build_messages(payload))["json_schema"]["schema"]
    properties = list(schema["$defs"]["TurnReview"]["properties"])

    assert properties.index("suggestions") < properties.index("explanation_ko")
