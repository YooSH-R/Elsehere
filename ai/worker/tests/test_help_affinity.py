"""도움을 제공한 학습자 줄도 기존 사건 호감 경로로 반영한다. 실제 모델 호출 없음."""

from test_api import judge_body
from test_judge_events import ORDER, answer

from app import npc_relationship as rel


def test_help_raises_affinity_once_per_conversation(client, provider):
    answer(provider, {**ORDER, "events": ["HELP"]})
    request = judge_body("I can carry those boxes for you.")

    first = client.post("/v1/judge", json=request)

    assert first.status_code == 200
    assert first.json()["events"] == ["HELP"]
    assert first.json()["affinity_delta"] == 6

    request["history"] = [
        {"who": "me", "text": request["text"], "events": ["HELP"]},
        {"who": "npc", "text": "Thank you, that would help."},
    ]
    request["text"] = "I can help with the other boxes too."

    repeated = client.post("/v1/judge", json=request).json()

    assert repeated["events"] == ["HELP"]
    assert repeated["affinity_delta"] == 0


def test_help_uses_the_existing_npc_event_scale_and_rounding():
    rules = rel.load_world_rules()

    assert rel.line_affinity_delta([], ["HELP"], {}, rules) == 6
    assert rel.line_affinity_delta(["GREETING"], ["HELP"], {}, rules) == 6
    assert rel.line_affinity_delta([], ["HELP", "HELP"], {}, rules) == 6
    assert rel.line_affinity_delta([], ["HELP"], {"eventScale": {"HELP": 1.5}}, rules) == 9


def test_help_on_a_rude_line_does_not_block_later_genuine_help(client, provider):
    answer(provider, {**ORDER, "events": ["HELP"]})
    request = judge_body("I can carry those boxes for you.")
    request["history"] = [
        {"who": "me", "text": "Let me help, idiot.", "rude": True, "events": ["HELP"]},
        {"who": "npc", "text": "Please be polite."},
    ]

    response = client.post("/v1/judge", json=request)

    assert response.status_code == 200
    assert response.json()["affinity_delta"] == 6
