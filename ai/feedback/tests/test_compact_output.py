"""축약 모델 응답을 기존 공개 계약으로 복원한다. 실제 모델 호출은 하지 않는다."""

import copy
import json
from pathlib import Path

import pytest
from run_evaluation import validation_diagnostic

from feedback.clients import FixtureClient
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
