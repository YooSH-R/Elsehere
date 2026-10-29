"""확정 무례 발화를 프로필·학습 표현의 원문으로 넘기지 않는다."""

import json

from test_turn import NPC

from conftest import ok


def test_memory_withholds_confirmed_rude_text_and_its_translation(client, provider):
    seen = {}
    old = {"name": "Alex", "claims": ["Prefers tea."], "events": [], "attitude": "neutral", "struggled": []}

    def answer(body):
        seen["messages"] = body["messages"]
        return ok(json.dumps(old))

    provider.answers["memory"] = answer
    response = client.post(
        "/v1/memory",
        json={
            "npc": NPC,
            "outcome": "Conversation ended after repeated confirmed rude turns.",
            "memory_card": old,
            "history": [
                {"who": "me", "text": "My favorite drink is tea."},
                {"who": "me", "text": "RAW_BLOCKED_TEXT", "english": "GENERATED_BLOCKED_TRANSLATION", "rude": True},
                {"who": "npc", "text": "Please speak respectfully."},
            ],
        },
    )
    assert response.status_code == 200
    assert response.json()["memory_card"] == old
    user_message = seen["messages"][1]["content"]
    assert "RAW_BLOCKED_TEXT" not in user_message
    assert "GENERATED_BLOCKED_TRANSLATION" not in user_message
    assert "[RUDE: content withheld]" in user_message
    assert "My favorite drink is tea." in user_message
    assert "Do not infer new rude classifications" in seen["messages"][0]["content"]
