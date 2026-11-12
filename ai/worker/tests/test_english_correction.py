"""영어 교정도 기존 표현 카드로 전달하며 실제 모델은 호출하지 않는다."""

import json

import httpx
import pytest
from test_api import judge_body
from test_judge_gpu import GPU, GPU_MODEL, PROFILE_INTENT, intent_by_model

from conftest import make_client, ok


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


@pytest.mark.parametrize("gpu_fails", [False, True])
def test_gpu_judgment_and_profile_fallback_both_return_the_correction(provider, gpu_fails):
    seen = []

    def answer(body):
        seen.append(body)
        if body["model"] == GPU_MODEL and gpu_fails:
            return httpx.Response(503, text="unavailable")
        return ok(json.dumps({"intents": ["unclear"], "events": [], "english_correction": "I want coffee."}))

    provider.answers["intent"] = intent_by_model({GPU_MODEL: answer, PROFILE_INTENT: answer})
    with make_client(provider, **GPU) as client:
        response = client.post("/v1/judge", json=judge_body("I wants coffee."))

    assert response.status_code == 200
    assert response.json()["translation"] == {"english": "I want coffee.", "error": None}
    expected_models = [GPU_MODEL, PROFILE_INTENT] if gpu_fails else [GPU_MODEL]
    assert provider.calls == [("intent", model) for model in expected_models]
    assert all(body["max_tokens"] == 200 for body in seen)
    assert all("english_correction" in body["response_format"]["json_schema"]["schema"]["required"] for body in seen)


# ---------------------------------------------------------------------
# 틀리진 않았지만 어색하거나 짧은 영어: '이럴 때 쓰면 좋아요!' 문장 (S15P31A305-847)
# ---------------------------------------------------------------------
LATTE_BETTER = ["Can I get a latte, please?", "I'd like a latte, please.", "Could I have a latte?"]


def test_short_or_awkward_english_gets_better_sentences_from_the_same_intent_call(client, provider):
    provider.answers["intent"] = lambda body: ok(
        json.dumps({"intents": ["unclear"], "events": [], "english_correction": None, "better_expressions": LATTE_BETTER})
    )

    result = client.post("/v1/judge", json=judge_body("Latte.")).json()

    assert result["better_expressions"] == LATTE_BETTER
    assert result["translation"] is None
    assert [step for step, _ in provider.calls] == ["intent"]


def test_better_sentences_keep_up_to_three_distinct_english_lines_unlike_the_learners(client, provider):
    raw = [
        " Can I get a latte, please? ",
        "can i get a latte please",
        "Latte.",
        "라떼 주세요",
        17,
        "",
        "!!!",
        *LATTE_BETTER[1:],
        "One latte, please.",
    ]
    provider.answers["intent"] = lambda body: ok(
        json.dumps({"intents": ["unclear"], "english_correction": None, "better_expressions": raw})
    )

    result = client.post("/v1/judge", json=judge_body("Latte.")).json()

    assert result["better_expressions"] == LATTE_BETTER


@pytest.mark.parametrize(
    ("text", "answer"),
    [
        ("I wants coffee.", {"english_correction": "I want coffee.", "better_expressions": ["I'd like a coffee."]}),
        ("라떼 하나요", {"english_correction": None, "better_expressions": LATTE_BETTER}),
        ("Latte.", {"english_correction": None}),
        ("Latte.", {"english_correction": None, "better_expressions": "Can I get a latte?"}),
    ],
)
def test_no_better_sentences_for_a_corrected_or_korean_line_or_a_missing_answer(client, provider, text, answer):
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))
    provider.answers["translate"] = lambda body: ok("One latte, please.")

    result = client.post("/v1/judge", json=judge_body(text)).json()

    assert result["better_expressions"] == []


def test_intent_schema_and_prompt_ask_for_better_sentences_only_for_awkward_or_short_english(client, provider):
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"intents": ["unclear"], "english_correction": None, "better_expressions": []}))

    provider.answers["intent"] = answer

    client.post("/v1/judge", json=judge_body("No, thanks."))

    schema = seen["response_format"]["json_schema"]["schema"]
    assert schema["properties"]["better_expressions"] == {"type": "array", "items": {"type": "string"}}
    assert "better_expressions" in schema["required"]
    system = seen["messages"][0]["content"]
    assert "no grammar error but sounds awkward or unnatural, or is too short" in system
    assert "return 3 natural sentences with the same meaning" in system
