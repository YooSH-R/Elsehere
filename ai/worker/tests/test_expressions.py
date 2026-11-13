"""표현 카드 칸(/v1/expressions): 영어 교정문·같은 뜻의 다른 표현·한국어 번역. 판정(/v1/judge)과 따로 동시에 불러
판정 출력을 줄이고(NPC 대답이 판정을 기다린다), 표현 카드는 Spring이 턴을 저장할 때 붙인다. 실제 모델은 호출하지 않는다."""

import json

import httpx
import pytest
from test_api import NPC, judge_body
from test_judge_gpu import GPU, GPU_MODEL, PROFILE_INTENT, intent_by_model

from conftest import make_client, ok


def expressions_body(text):
    return {"npc": NPC, "history": [{"who": "npc", "text": "Hi! What can I get for you?"}], "text": text}


def test_english_grammar_correction_comes_from_the_expressions_call(client, provider):
    provider.answers["expressions"] = lambda body: ok(
        json.dumps({"english_correction": "Yesterday I went to the cafe.", "better_expressions": []})
    )

    response = client.post("/v1/expressions", json=expressions_body("Yesterday I go to the cafe."))

    assert response.status_code == 200
    result = response.json()
    assert result["translation"] == {"english": "Yesterday I went to the cafe.", "error": None}
    assert [step for step, _ in provider.calls] == ["expressions"]


@pytest.mark.parametrize("correction", [None, "", "-", 17, [], {}, "문법 설명", "!!!"])
def test_absent_or_invalid_correction_shows_no_correction(client, provider, correction):
    provider.answers["expressions"] = lambda body: ok(json.dumps({"english_correction": correction, "better_expressions": []}))

    result = client.post("/v1/expressions", json=expressions_body("Hey there!")).json()

    assert result["translation"] is None
    assert [step for step, _ in provider.calls] == ["expressions"]


@pytest.mark.parametrize(
    ("original", "correction"),
    [("yeah thanks", "Yeah, thanks!"), ("I'm good", "I’m good."), ("For here, please.", "For here, please.")],
)
def test_identical_or_cosmetic_rewrites_are_not_shown(client, provider, original, correction):
    provider.answers["expressions"] = lambda body: ok(json.dumps({"english_correction": correction, "better_expressions": []}))

    result = client.post("/v1/expressions", json=expressions_body(original)).json()

    assert result["translation"] is None


def test_apostrophe_grammar_change_is_not_discarded_as_punctuation(client, provider):
    provider.answers["expressions"] = lambda body: ok(json.dumps({"english_correction": "It's raining.", "better_expressions": []}))

    result = client.post("/v1/expressions", json=expressions_body("Its raining.")).json()

    assert result["translation"]["english"] == "It's raining."


def test_korean_translation_takes_precedence_over_an_inapplicable_correction(client, provider):
    provider.answers["expressions"] = lambda body: ok(json.dumps({"english_correction": "Wrong correction.", "better_expressions": []}))
    provider.answers["translate"] = lambda body: ok("Can I leave my bag here?")

    result = client.post("/v1/expressions", json=expressions_body("Can I 가방 맡겨도 돼요?")).json()

    assert result["translation"] == {"english": "Can I leave my bag here?", "error": None}
    assert sorted(step for step, _ in provider.calls) == ["expressions", "translate"]


def test_failed_expressions_call_leaves_the_card_empty(client, provider):
    provider.answers["expressions"] = lambda body: httpx.Response(503, text="unavailable")

    response = client.post("/v1/expressions", json=expressions_body("I wants coffee."))

    assert response.status_code == 200
    assert response.json()["translation"] is None and response.json()["better_expressions"] == []
    assert all(step == "expressions" for step, _ in provider.calls)


def test_action_only_line_gets_no_expressions_without_a_model_call(client, provider):
    result = client.post("/v1/expressions", json=expressions_body("*웃는다*")).json()

    assert result == {"translation": None, "better_expressions": [], "usage": []}
    assert provider.calls == []


def test_expressions_schema_requests_a_nullable_correction(client, provider):
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"english_correction": None, "better_expressions": []}))

    provider.answers["expressions"] = answer

    client.post("/v1/expressions", json=expressions_body("No, thanks."))

    schema = seen["response_format"]["json_schema"]["schema"]
    assert schema["properties"]["english_correction"] == {"type": ["string", "null"]}
    assert set(schema["required"]) == {"english_correction", "better_expressions"}


def test_judge_no_longer_writes_the_expression_card(client, provider):
    """판정은 의도·사건만 쓴다. 표현 카드(교정·다른 표현·번역)는 /v1/expressions가 따로 만든다 — 판정 출력이 짧아야
    판정을 기다리는 NPC 대답이 빨리 나간다. 칸은 이전 Spring과 맞도록 비운 채 남긴다."""
    seen = {}

    def intent(body):
        seen.update(body)
        return ok(json.dumps({"intents": ["unclear"], "events": []}))

    provider.answers["intent"] = intent

    result = client.post("/v1/judge", json=judge_body("라떼 하나 I wants")).json()

    props = seen["response_format"]["json_schema"]["schema"]["properties"]
    assert "english_correction" not in props and "better_expressions" not in props
    assert "english_correction" not in seen["messages"][0]["content"]
    assert result["translation"] is None and result["better_expressions"] == []
    assert [step for step, _ in provider.calls] == ["intent"]  # 한국어가 섞여도 판정에서는 번역하지 않는다


@pytest.mark.parametrize("gpu_fails", [False, True])
def test_gpu_judgment_and_profile_fallback_ask_only_intents(provider, gpu_fails):
    seen = []

    def answer(body):
        seen.append(body)
        if body["model"] == GPU_MODEL and gpu_fails:
            return httpx.Response(503, text="unavailable")
        return ok(json.dumps({"intents": ["unclear"], "events": []}))

    provider.answers["intent"] = intent_by_model({GPU_MODEL: answer, PROFILE_INTENT: answer})
    with make_client(provider, **GPU) as client:
        response = client.post("/v1/judge", json=judge_body("I wants coffee."))

    assert response.status_code == 200
    assert all("english_correction" not in body["response_format"]["json_schema"]["schema"]["properties"] for body in seen)


# ---------------------------------------------------------------------
# 같은 뜻의 다른 표현: 모든 발화에 1~3개 (S15P31A305-847, S15P31A305-862)
# ---------------------------------------------------------------------
LATTE_BETTER = ["Can I get a latte, please?", "I'd like a latte, please.", "Could I have a latte?"]


def test_short_or_awkward_english_gets_better_sentences_from_the_expressions_call(client, provider):
    provider.answers["expressions"] = lambda body: ok(
        json.dumps({"intents": ["unclear"], "events": [], "english_correction": None, "better_expressions": LATTE_BETTER})
    )

    result = client.post("/v1/expressions", json=expressions_body("Latte.")).json()

    assert result["better_expressions"] == LATTE_BETTER
    assert result["translation"] is None
    assert [step for step, _ in provider.calls] == ["expressions"]


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
    provider.answers["expressions"] = lambda body: ok(
        json.dumps({"intents": ["unclear"], "english_correction": None, "better_expressions": raw})
    )

    result = client.post("/v1/expressions", json=expressions_body("Latte.")).json()

    assert result["better_expressions"] == LATTE_BETTER


@pytest.mark.parametrize(
    ("text", "answer"),
    [
        ("Latte.", {"english_correction": None}),
        ("Latte.", {"english_correction": None, "better_expressions": "Can I get a latte?"}),
    ],
)
def test_no_other_sentences_for_a_missing_answer(client, provider, text, answer):
    provider.answers["expressions"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))

    result = client.post("/v1/expressions", json=expressions_body(text)).json()

    assert result["better_expressions"] == []


def test_a_corrected_line_also_gets_other_sentences_unlike_its_correction(client, provider):
    # 고친 문장 아래에도 다른 표현을 접어 둔다(S15P31A305-862). 고친 문장과 같은 문장은 뺀다
    answer = {"english_correction": "I want coffee.", "better_expressions": ["I want coffee.", "I'd like a coffee."]}
    provider.answers["expressions"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))

    result = client.post("/v1/expressions", json=expressions_body("I wants coffee.")).json()

    assert result["translation"]["english"] == "I want coffee."
    assert result["better_expressions"] == ["I'd like a coffee."]


def test_a_korean_line_also_gets_other_sentences_unlike_its_translation(client, provider):
    # 한국어를 섞어 말해도 다른 표현을 준다. 영어 번역과 같은 문장은 뺀다
    answer = {"english_correction": None, "better_expressions": ["One latte, please.", "Can I get a latte?"]}
    provider.answers["expressions"] = lambda body: ok(json.dumps({"intents": ["unclear"], **answer}))
    provider.answers["translate"] = lambda body: ok("One latte, please.")

    result = client.post("/v1/expressions", json=expressions_body("라떼 하나요")).json()

    assert result["translation"]["english"] == "One latte, please."
    assert result["better_expressions"] == ["Can I get a latte?"]


def test_an_apostrophe_only_fix_offered_as_another_way_becomes_the_correction(client, provider):
    # "im sorry"를 맞게 말했다고 하고 "I'm sorry."를 다른 표현으로 권하지 않는다(S15P31A305-895).
    # 아포스트로피만 고친 학습자 말은 고친 문장으로 보이고, 다른 표현에서는 뺀다
    answer = {"english_correction": None, "better_expressions": ["I'm sorry.", "Sorry about that."]}
    provider.answers["expressions"] = lambda body: ok(json.dumps(answer))

    result = client.post("/v1/expressions", json=expressions_body("im sorry")).json()

    assert result["translation"] == {"english": "I'm sorry.", "error": None}
    assert result["better_expressions"] == ["Sorry about that."]


def test_other_ways_that_only_restore_an_apostrophe_of_the_correction_are_dropped(client, provider):
    answer = {"english_correction": "It's raining.", "better_expressions": ["Its raining!", "It is raining."]}
    provider.answers["expressions"] = lambda body: ok(json.dumps(answer))

    result = client.post("/v1/expressions", json=expressions_body("Its raining.")).json()

    assert result["translation"]["english"] == "It's raining."
    assert result["better_expressions"] == ["It is raining."]


def test_expressions_prompt_corrects_missing_apostrophes_and_rewrites_rude_lines_politely(client, provider):
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"english_correction": None, "better_expressions": []}))

    provider.answers["expressions"] = answer

    client.post("/v1/expressions", json=expressions_body("im sorry"))

    system = seen["messages"][0]["content"]
    assert "A missing apostrophe in a contraction" in system
    assert "If the line is rude or insulting" in system
    assert "polite ways to say what the learner feels or wants" in system


def test_expressions_prompt_asks_one_to_three_other_ways_for_every_line_with_room_to_write_them(client, provider):
    # '더 좋은 문장'이 아니라 같은 뜻의 다른 표현이다. 억지로 3개를 채우지 않고, 고친 문장·한국어가 섞인 말에도 준다.
    # 판정 출력에 고친 문장과 다른 표현이 함께 들어가도 잘리지 않게 출력 상한을 넉넉히 둔다
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"intents": ["unclear"], "english_correction": None, "better_expressions": []}))

    provider.answers["expressions"] = answer

    client.post("/v1/expressions", json=expressions_body("No, thanks."))

    system = seen["messages"][0]["content"]
    assert "other natural ways the learner could say the same thing" in system
    assert "including a line with a grammar error or Korean words" in system
    assert "Give 1 to 3, only as many as sound natural" in system
    assert "need not be better" in system
    assert "no grammar error but sounds awkward or unnatural, or is too short" not in system
    assert seen["max_tokens"] >= 320


def test_expressions_schema_requests_other_sentences_as_a_string_list(client, provider):
    seen = {}

    def answer(body):
        seen.update(body)
        return ok(json.dumps({"intents": ["unclear"], "english_correction": None, "better_expressions": []}))

    provider.answers["expressions"] = answer

    client.post("/v1/expressions", json=expressions_body("No, thanks."))

    schema = seen["response_format"]["json_schema"]["schema"]
    assert schema["properties"]["better_expressions"] == {"type": "array", "items": {"type": "string"}}
    assert "better_expressions" in schema["required"]
    system = seen["messages"][0]["content"]
    # 상대 대답에 맞는 짧은 답(For here, please.)에도 완전한 문장을 준다
    assert 'Short answers and fragments such as "For here, please." also get them' in system
    assert "A short answer that fully and naturally answers the partner" not in system
