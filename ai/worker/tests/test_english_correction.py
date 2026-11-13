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
    assert all(body["max_tokens"] == 320 for body in seen)
    assert all("english_correction" in body["response_format"]["json_schema"]["schema"]["required"] for body in seen)


# ---------------------------------------------------------------------
# 같은 뜻의 다른 표현: 모든 발화에 1~3개 (S15P31A305-847, S15P31A305-862)
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
        ("Latte.", {"english_correction": None}),
        ("Latte.", {"english_correction": None, "better_expressions": "Can I get a latte?"}),
    ],
)
def test_no_other_sentences_for_a_missing_answer(client, provider, text, answer):
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))

    result = client.post("/v1/judge", json=judge_body(text)).json()

    assert result["better_expressions"] == []


def test_a_corrected_line_also_gets_other_sentences_unlike_its_correction(client, provider):
    # 고친 문장 아래에도 다른 표현을 접어 둔다(S15P31A305-862). 고친 문장과 같은 문장은 뺀다
    answer = {"english_correction": "I want coffee.", "better_expressions": ["I want coffee.", "I'd like a coffee."]}
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))

    result = client.post("/v1/judge", json=judge_body("I wants coffee.")).json()

    assert result["translation"]["english"] == "I want coffee."
    assert result["better_expressions"] == ["I'd like a coffee."]


def test_a_korean_line_also_gets_other_sentences_unlike_its_translation(client, provider):
    # 한국어를 섞어 말해도 다른 표현을 준다. 영어 번역과 같은 문장은 뺀다
    answer = {"english_correction": None, "better_expressions": ["One latte, please.", "Can I get a latte?"]}
    provider.answers["intent"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))
    provider.answers["translate"] = lambda body: ok("One latte, please.")

    result = client.post("/v1/judge", json=judge_body("라떼 하나요")).json()

    assert result["translation"]["english"] == "One latte, please."
    assert result["better_expressions"] == ["Can I get a latte?"]


def test_intent_prompt_asks_one_to_three_other_ways_for_every_line_with_room_to_write_them(client, provider):
    # '더 좋은 문장'이 아니라 같은 뜻의 다른 표현이다. 억지로 3개를 채우지 않고, 고친 문장·한국어가 섞인 말에도 준다.
    # 판정 출력에 고친 문장과 다른 표현이 함께 들어가도 잘리지 않게 출력 상한을 넉넉히 둔다
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"intents": ["unclear"], "english_correction": None, "better_expressions": []}))

    provider.answers["intent"] = answer

    client.post("/v1/judge", json=judge_body("No, thanks."))

    system = seen["messages"][0]["content"]
    assert "other natural ways the learner could say the same thing" in system
    assert "including a line with a grammar error or Korean words" in system
    assert "Give 1 to 3, only as many as sound natural" in system
    assert "need not be better" in system
    assert "no grammar error but sounds awkward or unnatural, or is too short" not in system
    assert seen["max_tokens"] >= 320


def test_intent_schema_requests_other_sentences_as_a_string_list(client, provider):
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
    # 상대 대답에 맞는 짧은 답(For here, please.)에도 완전한 문장을 준다
    assert 'Short answers and fragments such as "For here, please." also get them' in system
    assert "A short answer that fully and naturally answers the partner" not in system
