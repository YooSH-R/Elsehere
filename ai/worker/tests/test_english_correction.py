"""영어 교정도 기존 표현 카드로 전달하며 실제 모델은 호출하지 않는다."""

import json

import httpx
import pytest
from test_api import judge_body

from conftest import ok


def test_english_grammar_correction_uses_the_existing_intent_call(client, provider):
    provider.answers["intent"] = lambda body: ok(
        json.dumps(
            {
                "intents": ["unclear"],
                "events": [],
                "english_correction": "Yesterday I went to the cafe.",
            }
        )
    )

    response = client.post("/v1/judge", json=judge_body("Yesterday I go to the cafe."))

    assert response.status_code == 200
    result = response.json()
    assert result["text"] == "Yesterday I go to the cafe."
    assert result["translation"] == {"english": "Yesterday I went to the cafe.", "error": None}
    assert result["intent"]["id"] == "unclear"
    assert [step for step, _ in provider.calls] == ["intent"]


@pytest.mark.parametrize("correction", [None, "", "-", 17, [], {}, "문법 설명", "!!!"])
def test_absent_or_invalid_correction_does_not_disrupt_intent_and_events(client, provider, correction):
    provider.answers["intent"] = lambda body: ok(
        json.dumps({"intents": ["unclear"], "events": ["GREETING"], "english_correction": correction})
    )

    result = client.post("/v1/judge", json=judge_body("Hey there!")).json()

    assert result["translation"] is None
    assert result["intent"]["id"] == "unclear"
    assert result["events"] == ["GREETING"]
    assert [step for step, _ in provider.calls] == ["intent"]


@pytest.mark.parametrize(
    ("original", "correction"),
    [("yeah thanks", "Yeah, thanks!"), ("I'm good", "I’m good."), ("For here, please.", "For here, please.")],
)
def test_identical_or_cosmetic_rewrites_are_not_shown(client, provider, original, correction):
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], "events": [], "english_correction": correction}))

    result = client.post("/v1/judge", json=judge_body(original)).json()

    assert result["translation"] is None
    assert result["text"] == original


def test_apostrophe_grammar_change_is_not_discarded_as_punctuation(client, provider):
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], "english_correction": "It's raining."}))

    result = client.post("/v1/judge", json=judge_body("Its raining.")).json()

    assert result["translation"]["english"] == "It's raining."


def test_korean_translation_takes_precedence_over_an_inapplicable_correction(client, provider):
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], "english_correction": "Wrong correction."}))
    provider.answers["translate"] = lambda body: ok("Can I leave my bag here?")

    result = client.post("/v1/judge", json=judge_body("Can I 가방 맡겨도 돼요?")).json()

    assert result["translation"] == {"english": "Can I leave my bag here?", "error": None}
    assert sorted(step for step, _ in provider.calls) == ["intent", "translate"]


def test_failed_english_judgment_keeps_the_turn_without_a_correction(client, provider):
    provider.answers["intent"] = lambda body: httpx.Response(503, text="unavailable")

    response = client.post("/v1/judge", json=judge_body("I wants coffee."))

    assert response.status_code == 200
    assert response.json()["text"] == "I wants coffee."
    assert response.json()["translation"] is None
    assert response.json()["intent"]["error"]
    assert all(step == "intent" for step, _ in provider.calls)


def test_intent_schema_requests_a_nullable_correction_and_preserves_existing_fields(client, provider):
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"intents": ["unclear"], "english_correction": None}))

    provider.answers["intent"] = answer

    client.post("/v1/judge", json=judge_body("No, thanks."))

    schema = seen["response_format"]["json_schema"]["schema"]
    assert schema["properties"]["english_correction"] == {"type": ["string", "null"]}
    assert "english_correction" in schema["required"]
    assert {"intents", "items", "events", "choice"} <= schema["properties"].keys()
