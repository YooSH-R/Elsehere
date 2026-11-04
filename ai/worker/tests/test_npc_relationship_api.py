"""대화 뒤 관계 갱신 API(/v1/npc/relationship). 백엔드가 정상 종료된 대화마다 /v1/memory와 같은 자리에서 부른다.
호감은 대화 중에 발화마다 이미 바꿨으므로(사건은 /v1/judge, 무례는 /v1/guard의 affinity_delta) 여기서는 알아봄만 계산한다.
공통 규칙으로 코드가 계산하고 모델을 부르지 않는다(app.npc_relationship). 워커는 저장하지 않는다."""

import pytest
from test_npc_reply_profile import NPC, profile_of

from app import npc_persona
from app import npc_relationship as rel
from app.npc_profile_models import NpcProfile

PATH = "/v1/npc/relationship"
RULES = rel.load_world_rules()

HISTORY = [
    {"who": "npc", "text": "Hi! What can I get you?"},
    {"who": "me", "text": "Hello! One latte, please. Your cafe is so nice.", "events": ["GREETING", "COMPLIMENT"]},
]


def update(client, npc: dict = NPC, **body) -> dict:
    r = client.post(PATH, json={"npc": npc, "history": HISTORY, **body})
    assert r.status_code == 200, r.text
    return r.json()


def test_after_a_conversation_only_recognition_changes_without_a_model_call(client, provider):
    """새 값이 아니라 변화량을 돌려준다. 백엔드가 저장값에 더하므로 종료 처리가 겹쳐도 앞 대화 결과를 덮어쓰지 않는다.
    칭찬한 대화여도 호감 변화량은 없다(그 턴에 이미 더함)."""
    before = {"recognition": 30, "affinity": 10, "days_since_last_talk": 1}

    body = update(client, relationship=before)

    assert body == rel.after_conversation(before, {}, RULES) == {"recognition_delta": body["recognition_delta"]}
    assert provider.calls == []


def test_a_first_meeting_needs_no_relationship(client):
    assert update(client) == rel.after_conversation(None, {}, RULES)


def test_the_npc_card_sets_how_fast_it_knows_them(client):
    """인격 카드의 성격(친화성)으로 알아보는 속도가 다르다. 친화성이 낮은 호텔 직원은 기본보다 천천히 알아본다."""
    profile = profile_of("HOTEL_STAFF")
    rates = rel.npc_rates(NpcProfile.model_validate(profile), RULES, npc_persona.load_behaviors())

    body = update(client, {**NPC, "profile": profile})

    assert body == rel.after_conversation(None, rates, RULES)
    assert body["recognition_delta"] < update(client)["recognition_delta"]


@pytest.mark.parametrize("history", [[{"who": "npc", "text": "Hi!"}], []])
def test_a_conversation_where_the_learner_said_nothing_is_not_counted(client, history):
    r = client.post(PATH, json={"npc": NPC, "history": history})

    assert r.status_code == 400
