"""의도 판정(/v1/judge)의 줄 사건. 의도 판정 호출이 학습자 줄의 대화 사건(인사·잡담·사적인 질문·사과·칭찬·용건 끝냄·작별)도 함께 고른다.
백엔드는 줄마다 저장해 history[].events로 보내고, 워커가 대화 중 기분과 대화 뒤 관계에 쓴다(app.npc_relationship).
사건 정의는 이정훈 님 사건 테스트셋 가이드 v1.0을 따른다. 무례는 /v1/guard만 판정한다. 가짜 공급자라 진짜 호출 없음."""

import json

from test_api import judge_body

from conftest import ok

LINE_EVENTS = ["GREETING", "SMALL_TALK", "PERSONAL_QUESTION", "APOLOGY", "COMPLIMENT", "TASK_DONE", "LEAVE"]
ORDER = {"intents": ["order_items"], "items": [{"name": "latte", "iced": False, "qty": 1}], "choice": "none"}


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

    assert body["intent"]["id"] == "unclear" and body["events"] == []


def test_an_action_only_line_has_no_events_like_no_intent(client):
    body = judge(client, "*waves*")

    assert body["dropped"] is True and body["intent"] is None and body["events"] is None


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
