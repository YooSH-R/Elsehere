"""대화가 끝난 뒤 이번 대화의 인상으로 호감을 올리는 /v1/memory 계약. 실제 외부 모델은 부르지 않는다.
기억 카드를 만드는 같은 호출에서 모델이 이번 대화만의 인상(good·neutral·bad)을 고르고, 좋은 대화면 워커가 공통 규칙의 값만큼 호감 변화량을 돌려준다."""

import json

import pytest

from app import npc_relationship as rel
from conftest import ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista.", "scene": "Cafe."}
CARD = {"name": "Alex", "claims": [], "events": [], "attitude": "friendly", "struggled": []}
RULES = rel.load_world_rules()["relationship"]
GOOD_BONUS = RULES["goodConversationAffinity"]
MIN_LINES = RULES["goodConversationMinLearnerLines"]


def conversation(learner_lines: int, rude_at: int | None = None) -> list[dict]:
    history = []
    for index in range(learner_lines):
        history.append({"who": "me", "text": f"Line {index}.", "rude": index == rude_at})
        history.append({"who": "npc", "text": "Sure!"})
    return history


def remember(client, provider, history: list[dict], answer: dict):
    provider.answers["memory"] = lambda body: ok(json.dumps(answer))
    return client.post("/v1/memory", json={"npc": NPC, "outcome": "The learner ended the conversation normally.", "history": history})


def test_good_conversation_bonus_is_three_after_three_learner_lines():
    """사용자 결정(2026-11-05): 좋은 대화 한 번에 +3, 학습자가 3줄 이상 말한 대화만."""
    assert (GOOD_BONUS, MIN_LINES) == (3, 3)


def test_a_good_conversation_raises_affinity_and_keeps_the_card(client, provider):
    response = remember(client, provider, conversation(MIN_LINES), CARD | {"impression": "good"})

    assert response.status_code == 200
    body = response.json()
    assert body["memory_card"] == CARD
    assert body["impression"] == "good"
    assert body["affinity_delta"] == GOOD_BONUS


def test_a_short_good_conversation_does_not_raise_affinity(client, provider):
    """인사 한마디 하고 나가는 대화를 반복해 호감을 쌓지 못하게 한다."""
    response = remember(client, provider, conversation(MIN_LINES - 1), CARD | {"impression": "good"})

    assert response.status_code == 200 and response.json()["affinity_delta"] == 0


@pytest.mark.parametrize("impression", ["neutral", "bad"])
def test_only_a_good_conversation_raises_affinity_and_none_lowers_it(client, provider, impression):
    """나쁜 대화도 깎지 않는다. 무례는 대화 중에 이미 깎았다."""
    response = remember(client, provider, conversation(MIN_LINES + 2), CARD | {"impression": impression})

    assert response.status_code == 200 and response.json()["affinity_delta"] == 0


def test_a_conversation_with_a_confirmed_rude_line_gets_no_bonus(client, provider):
    response = remember(client, provider, conversation(MIN_LINES + 2, rude_at=1), CARD | {"impression": "good"})

    assert response.status_code == 200 and response.json()["affinity_delta"] == 0


@pytest.mark.parametrize("impression", [None, "great", 3])
def test_a_missing_or_unknown_impression_is_neutral_and_still_saves_the_card(client, provider, impression):
    """인상 칸 하나 때문에 기억 카드 갱신까지 실패하지 않게 한다."""
    answer = CARD if impression is None else CARD | {"impression": impression}

    response = remember(client, provider, conversation(MIN_LINES), answer)

    assert response.status_code == 200
    body = response.json()
    assert body["memory_card"] == CARD
    assert body["impression"] == "neutral" and body["affinity_delta"] == 0


def test_the_model_is_asked_for_the_impression_of_this_conversation_only(client, provider):
    seen = {}

    def answer(body):
        seen["schema"] = body["response_format"]["json_schema"]["schema"]
        seen["system"] = body["messages"][0]["content"]
        return ok(json.dumps(CARD | {"impression": "neutral"}))

    provider.answers["memory"] = answer
    client.post("/v1/memory", json={"npc": NPC, "outcome": "o", "history": conversation(1)})

    assert seen["schema"]["properties"]["impression"]["enum"] == ["good", "neutral", "bad"]
    assert "impression" in seen["schema"]["required"]
    assert "impression" in seen["system"]
