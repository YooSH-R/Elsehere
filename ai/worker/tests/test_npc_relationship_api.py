"""대화 뒤 관계 갱신 API(/v1/npc/relationship). 백엔드가 정상 종료된 대화마다 /v1/memory와 같은 자리에서 부른다.
사건은 의도 판정(/v1/judge)이 줄마다 고른 history[].events, 무례는 Spring이 확정한 줄(rude)로 센다.
새 알아봄·호감은 공통 규칙으로 코드가 계산하고 모델을 부르지 않는다(app.npc_relationship). 워커는 저장하지 않는다."""

import pytest
from test_npc_reply_profile import NPC, profile_of

from app import npc_relationship as rel

PATH = "/v1/npc/relationship"
RULES = rel.load_world_rules()


def me(text: str, *events: str, rude: bool = False) -> dict:
    return {"who": "me", "text": text, "rude": rude, "events": list(events)}


HISTORY = [
    {"who": "npc", "text": "Hi! What can I get you?"},
    me("Hello! One latte, please. Your cafe is so nice.", "GREETING", "COMPLIMENT"),
    {"who": "npc", "text": "Thank you! Here you go."},
    me("That's all. Bye!", "TASK_DONE", "LEAVE"),
]


def update(client, npc: dict = NPC, **body) -> dict:
    r = client.post(PATH, json={"npc": npc, "history": HISTORY, **body})
    assert r.status_code == 200, r.text
    return r.json()


def test_the_line_events_and_the_rules_give_the_changes_without_a_model_call(client, provider):
    """새 값이 아니라 변화량을 돌려준다. 백엔드가 저장값에 더하므로 종료 처리가 겹쳐도 앞 대화 결과를 덮어쓰지 않는다."""
    before = {"recognition": 30, "affinity": 10, "days_since_last_talk": 1}

    body = update(client, relationship=before)

    events = ["GREETING", "COMPLIMENT", "TASK_DONE", "LEAVE"]
    assert body == {**rel.after_conversation(before, events, {}, RULES), "events": events}
    assert provider.calls == []


def test_a_first_meeting_needs_no_relationship(client):
    body = update(client)

    assert body == {**rel.after_conversation(None, body["events"], {}, RULES), "events": body["events"]}


def test_the_same_event_counts_once_per_conversation(client):
    """칭찬을 반복해도 호감은 한 번만큼 오른다."""
    twice = [*HISTORY, me("You are the best barista!", "COMPLIMENT")]

    assert update(client, history=twice) == update(client)


def test_rude_lines_are_left_out_because_the_guard_already_took_affinity_off(client):
    """무례는 발화마다 /v1/guard의 affinity_delta로 이미 깎았으므로 여기서는 세지 않는다(이중 감점 방지).
    무례한 줄의 다른 사건(욕하면서 칭찬 등)도 버린다(이정훈 님 협의)."""
    rude = me("You idiot, your cafe is nice though.", "COMPLIMENT", rude=True)
    polite = [HISTORY[0], me("One latte.", "TASK_DONE")]
    profile = {**NPC, "profile": profile_of("HOTEL_STAFF")}

    body = update(client, profile, history=[*polite, rude, rude])

    assert body["events"] == ["TASK_DONE"]
    assert body == update(client, profile, history=polite)


def test_events_outside_the_list_are_dropped(client):
    """목록 밖 값과 줄 사건으로 온 RUDE는 버린다."""
    history = [HISTORY[0], me("Hi!", "GREETING", "SHOUTING", "RUDE")]

    body = update(client, history=history)

    assert body == {**rel.after_conversation(None, ["GREETING"], {}, RULES), "events": ["GREETING"]}


@pytest.mark.parametrize("history", [[{"who": "npc", "text": "Hi!"}], []])
def test_a_conversation_where_the_learner_said_nothing_is_not_counted(client, history):
    r = client.post(PATH, json={"npc": NPC, "history": history})

    assert r.status_code == 400
