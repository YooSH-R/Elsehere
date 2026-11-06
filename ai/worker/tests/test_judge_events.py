"""의도 판정(/v1/judge)의 줄 사건. 의도 판정 호출이 학습자 줄의 대화 사건(인사·잡담·사적인 질문·사과·칭찬·용건 끝냄·작별)도 함께 고른다.
그 사건으로 바로 오를 호감(affinity_delta)도 함께 돌려주고, 백엔드는 무례가 아닌 턴을 저장하면서 더한다(app.npc_relationship).
백엔드는 줄 사건을 저장해 history[].events로 보내고, 워커가 대화 중 기분과 이미 센 사건을 거르는 데 쓴다.
사건 정의는 이정훈 님 사건 테스트셋 가이드 v1.0을 따른다. 무례는 /v1/guard만 판정한다. 가짜 공급자라 진짜 호출 없음."""

import json

from test_api import judge_body
from test_npc_reply_profile import profile_of

from app import npc_relationship as rel
from conftest import ok

LINE_EVENTS = ["GREETING", "SMALL_TALK", "PERSONAL_QUESTION", "APOLOGY", "COMPLIMENT", "HELP", "TASK_DONE", "LEAVE"]
ORDER = {"intents": ["order_items"], "items": [{"name": "latte", "iced": False, "qty": 1}], "choice": "none"}
RULES = rel.load_world_rules()
EVENTS = RULES["conversationEvents"]


def answer(provider, output: dict | str) -> dict:
    """의도 판정 모델이 output을 답하게 하고, 모델에 간 요청을 seen["body"]에 남긴다."""
    seen = {}

    def intent(body):
        seen["body"] = body
        return ok(output if isinstance(output, str) else json.dumps(output))

    provider.answers["intent"] = intent
    return seen


def judge(client, text: str = "Hi! One latte, please. I love this cafe.") -> dict:
    r = client.post("/v1/judge", json=judge_body(text))
    assert r.status_code == 200, r.text
    return r.json()


def test_the_intent_call_also_picks_the_events_of_the_line(client, provider):
    seen = answer(provider, {**ORDER, "events": ["GREETING", "COMPLIMENT"]})

    body = judge(client)

    assert body["events"] == ["GREETING", "COMPLIMENT"]
    assert body["intent"]["id"] == "order_items"
    assert [u["step"] for u in body["usage"]] == ["intent"]  # 모델 호출은 늘지 않는다
    schema = seen["body"]["response_format"]["json_schema"]["schema"]
    assert schema["properties"]["events"]["items"]["enum"] == LINE_EVENTS
    assert "events" in schema["required"]


def test_rudeness_and_values_outside_the_list_are_dropped(client, provider):
    answer(provider, {**ORDER, "events": ["RUDE", "SHOUTING", "APOLOGY", "APOLOGY", 3]})

    assert judge(client, "Sorry, one latte please.")["events"] == ["APOLOGY"]


def test_an_output_without_events_gives_an_empty_list(client, provider):
    """형식 강제가 풀린 대체 모드처럼 events가 없거나 목록이 아니면 사건 없음. 의도는 그대로 쓴다."""
    answer(provider, {"intent": "order_items", "items": [], "events": "GREETING"})

    body = judge(client, "one latte")

    assert body["events"] == [] and body["intent"]["id"] == "order_items"


def test_a_failed_intent_gives_no_events(client, provider):
    answer(provider, "not json")

    body = judge(client)

    assert body["intent"]["id"] == "unclear" and body["events"] == [] and body["affinity_delta"] == 0


def test_an_action_only_line_has_no_events_like_no_intent(client):
    body = judge(client, "*waves*")

    assert body["dropped"] is True and body["intent"] is None and body["events"] is None and body["affinity_delta"] is None


def test_the_events_of_the_line_raise_affinity_right_away(client, provider):
    """칭찬·인사로 오르는 호감도 무례 감점처럼 그 턴에 바로 더한다. 사건 인상 × affinityFollowRate (인격 카드 없으면 기본 배수)."""
    answer(provider, {**ORDER, "events": ["GREETING", "COMPLIMENT"]})

    body = judge(client)

    greeting, compliment = (EVENTS[name]["impression"] for name in ("GREETING", "COMPLIMENT"))
    assert body["affinity_delta"] == round((greeting + compliment) * RULES["relationship"]["affinityFollowRate"]) > 0


def test_an_event_said_earlier_in_the_conversation_does_not_raise_it_again(client, provider):
    """칭찬을 반복해도 한 번만 오른다. 백엔드가 보내는 이력(최근 40줄)의 학습자 줄 사건으로 거른다."""
    answer(provider, {**ORDER, "events": ["COMPLIMENT"]})
    earlier = [{"who": "me", "text": "Your cafe is lovely.", "events": ["COMPLIMENT"]}, {"who": "npc", "text": "Thanks!"}]

    body = client.post("/v1/judge", json=judge_body("I love this cafe.", history=earlier)).json()

    assert body["events"] == ["COMPLIMENT"] and body["affinity_delta"] == 0


def test_events_on_an_earlier_rude_line_were_not_counted(client, provider):
    """무례로 확정된 줄의 다른 사건(욕하면서 칭찬 등)은 세지 않았으므로, 그 뒤의 진짜 칭찬은 오른다."""
    answer(provider, {**ORDER, "events": ["COMPLIMENT"]})
    earlier = [
        {"who": "me", "text": "You idiot, nice cafe though.", "rude": True, "events": ["COMPLIMENT"]},
        {"who": "npc", "text": "Excuse me?"},
    ]

    body = client.post("/v1/judge", json=judge_body("Sorry. I really like this cafe.", history=earlier)).json()

    assert body["affinity_delta"] == rel.line_affinity_delta([], ["COMPLIMENT"], {}, RULES) > 0


def test_the_npc_card_sets_how_much_an_event_moves_it(client, provider):
    """인격 카드의 사건 반응 크기(eventScale)를 곱한다. 행인 NPC는 사과를 크게 받아 준다."""
    answer(provider, {**ORDER, "events": ["APOLOGY"]})
    plain = judge_body("Sorry about that.")
    passerby = {**plain, "npc": {**plain["npc"], "profile": profile_of("TOWN_PASSERBY")}}

    with_card = client.post("/v1/judge", json=passerby).json()["affinity_delta"]

    assert with_card > judge(client, "Sorry about that.")["affinity_delta"] > 0


def test_the_prompt_carries_the_event_guide(client, provider):
    """가이드 v1.0: 의례적 감사는 칭찬이 아님, 되묻기·영어 실력 사과는 사과가 아님, 사적인 질문은 잡담과 겹쳐 세지 않음,
    용건 끝냄은 말로 드러낼 때만, 한국어도 뜻대로, 영어 실수는 문제 삼지 않음, 무례는 여기서 판정하지 않음."""
    seen = answer(provider, {**ORDER, "events": []})

    judge(client)

    prompt = seen["body"]["messages"][1]["content"]
    assert all(event in prompt for event in LINE_EVENTS)
    for words in [
        'plain "thanks"',
        '"sorry?"',
        "apologizing for their English",
        "not SMALL_TALK",
        "That's all",
        "Korean",
        "English mistakes",
    ]:
        assert words in prompt, words
    assert "rudeness" in prompt


def test_missing_bag_details_can_be_completed_across_learner_turns(client, provider):
    earlier = "I think I lost my small bag."

    def intent(body):
        prompt = body["messages"][1]["content"]
        recognized = earlier in prompt and "at the cafe" in prompt
        return ok(json.dumps({"intents": ["report_left_bag"] if recognized else ["unclear"], "events": []}))

    provider.answers["intent"] = intent
    request = judge_body(
        "I think I left it at the cafe.",
        state={
            "id": "first_day_2",
            "desc": "Explain the missing bag and where it was left.",
            "intents": [{"id": "report_left_bag", "desc": "Explain which bag is missing AND where it was left."}],
            "accumulate_history": True,
        },
        history=[{"who": "me", "text": earlier}, {"who": "npc", "text": "Where did you leave it?"}],
    )

    response = client.post("/v1/judge", json=request)

    assert response.status_code == 200
    assert response.json()["intent"]["ids"] == ["report_left_bag"]
