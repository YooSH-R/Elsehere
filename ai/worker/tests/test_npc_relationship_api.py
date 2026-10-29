"""대화 뒤 관계 갱신 API(/v1/npc/relationship). 백엔드가 정상 종료된 대화마다 /v1/memory와 같은 자리에서 부른다.
LLM이 이번 대화의 사건(인사·잡담·사과·칭찬·용건 완료 등)만 고르고, 무례는 Spring이 확정한 줄로 센다.
새 알아봄·호감은 공통 규칙으로 코드가 계산한다(app.npc_relationship). 워커는 저장하지 않는다. 가짜 공급자라 진짜 호출 없음."""

import json

import pytest
from test_npc_reply_profile import NPC, profile_of

from app import npc_relationship as rel
from conftest import ok

PATH = "/v1/npc/relationship"
RULES = rel.load_world_rules()
HISTORY = [
    {"who": "npc", "text": "Hi! What can I get you?"},
    {"who": "me", "text": "Hello! One latte, please. Your cafe is so nice."},
    {"who": "npc", "text": "Thank you! Here you go."},
    {"who": "me", "text": "Thanks, bye!"},
]
RUDE_LINE = {"who": "me", "text": "You are so stupid and slow", "rude": True}


def judged(provider, events: list[str]):
    seen = {}

    def answer(body):
        seen["body"] = body
        return ok(json.dumps({"events": events}))

    provider.answers["relationship"] = answer
    return seen


def update(client, npc: dict = NPC, **body) -> dict:
    r = client.post(PATH, json={"npc": npc, "history": HISTORY, **body})
    assert r.status_code == 200, r.text
    return r.json()


def test_the_judged_events_and_the_rules_give_the_new_relationship(client, provider):
    events = ["GREETING", "COMPLIMENT", "TASK_DONE", "LEAVE"]
    judged(provider, events)
    before = {"recognition": 30, "affinity": 10, "days_since_last_talk": 1}

    body = update(client, relationship=before)

    assert body["events"] == events
    assert body["relationship"] == rel.after_conversation(before, events, 0, {}, RULES)
    assert body["usage"] and body["usage"][0]["step"] == "relationship"


def test_a_first_meeting_needs_no_relationship(client, provider):
    judged(provider, ["GREETING"])

    body = update(client)

    assert body["relationship"] == rel.after_conversation(None, ["GREETING"], 0, {}, RULES)


def test_rudeness_comes_from_spring_and_its_words_never_reach_the_model(client, provider):
    """무례는 확정된 줄마다 센다. 모델은 무례를 판정하지 않고, 원문 대신 표식만 본다."""
    seen = judged(provider, ["RUDE", "GREETING"])  # 모델이 RUDE를 골라도 세지 않는다

    body = update(client, history=[*HISTORY, RUDE_LINE, RUDE_LINE])

    assert body["events"] == ["GREETING"]
    assert body["relationship"] == rel.after_conversation(None, ["GREETING"], 2, {}, RULES)
    sent = json.dumps(seen["body"]["messages"])
    assert "stupid" not in sent and "[RUDE: content withheld]" in sent


def test_events_outside_the_shared_list_are_dropped(client, provider):
    judged(provider, ["GREETING", "SHOUTING", "GREETING"])

    assert update(client)["events"] == ["GREETING"]


def with_neuroticism(profile: dict, value: float) -> dict:
    personality = profile["personality"]
    return {**profile, "personality": {**personality, "dimensions": {**personality["dimensions"], "neuroticism": value}}}


def test_the_npc_temperament_changes_how_much_rudeness_hurts(client, provider):
    """신경성이 높은 NPC는 같은 무례에 호감이 더 깎인다. 요청의 인격 카드로 계산한 배수를 쓴다."""
    judged(provider, [])
    history = [RUDE_LINE, RUDE_LINE, RUDE_LINE]
    jake = profile_of("HOTEL_STAFF")
    touchy = update(client, {**NPC, "profile": with_neuroticism(jake, 1.0)}, history=history)["relationship"]["affinity"]
    calm = update(client, {**NPC, "profile": with_neuroticism(jake, 0.0)}, history=history)["relationship"]["affinity"]

    assert touchy < calm < 0


def test_the_model_is_asked_only_for_the_shared_events_and_never_to_judge_english(client, provider):
    seen = judged(provider, [])

    update(client)

    schema = json.dumps(seen["body"]["response_format"])
    assert "COMPLIMENT" in schema and "RUDE" not in schema
    assert "mistakes" in seen["body"]["messages"][0]["content"]  # 영어 실수를 깎을 일로 보지 않는다 (요구사항 REQ-24)


@pytest.mark.parametrize("output", ["not json", json.dumps({"event": ["GREETING"]}), json.dumps({"events": "GREETING"})])
def test_an_output_without_an_events_list_is_an_invalid_output(client, provider, output):
    """판정에 실패하면 502. 백엔드는 이전 관계를 그대로 둔다(/v1/memory와 같다)."""
    provider.answers["relationship"] = lambda body: ok(output)

    r = client.post(PATH, json={"npc": NPC, "history": HISTORY})

    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


def test_a_conversation_where_the_learner_said_nothing_is_not_counted(client):
    r = client.post(PATH, json={"npc": NPC, "history": [{"who": "npc", "text": "Hi!"}]})

    assert r.status_code == 400
