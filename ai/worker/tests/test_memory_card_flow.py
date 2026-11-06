"""공유된 카드형 /v1/memory 계약. 실제 외부 모델·DB를 사용하지 않는다."""

import asyncio
import copy
import json

import httpx
import pytest

from conftest import make_client, ok, stream

NPC = {"id": "emma", "name": "Emma", "role": "the barista.", "scene": "Cafe."}
OLD = {"name": "Alex", "claims": ["says coffee is their favorite drink"], "events": [], "attitude": "neutral", "struggled": []}


def memory_request(card=None, text="I now prefer tea."):
    return {
        "npc": NPC,
        "outcome": "Conversation ended normally.",
        "history": [{"who": "me", "text": text}],
        "memory_card": copy.deepcopy(card),
    }


@pytest.mark.parametrize(
    "problem",
    [
        "missing_fields",
        "claims_string",
        "events_number",
        "struggled_object",
        "list_nonstring",
        "name_number",
        "name_object",
        "attitude_object",
    ],
)
def test_invalid_model_card_is_502_not_a_successful_empty_or_coerced_card(client, provider, problem):
    broken = copy.deepcopy(OLD)
    if problem == "missing_fields":
        broken = {"name": "Alex"}
    elif problem == "claims_string":
        broken["claims"] = "not a list"
    elif problem == "events_number":
        broken["events"] = 17
    elif problem == "struggled_object":
        broken["struggled"] = {"wrong": "type"}
    elif problem == "list_nonstring":
        broken["claims"] = [17, {"injected": "object"}]
    elif problem == "name_number":
        broken["name"] = 42
    elif problem == "name_object":
        broken["name"] = {"not": "a name"}
    else:
        broken["attitude"] = {"not": "a tone"}
    provider.answers["memory"] = lambda body: ok(json.dumps(broken))
    response = client.post("/v1/memory", json=memory_request(OLD))
    assert response.status_code == 502
    assert response.json()["error"]["code"] == "invalid_output"
    assert "memory_card" not in response.json()
    assert len(provider.calls) == 1


@pytest.mark.parametrize("problem", ["duplicate_json_key", "nonstandard_json_constant", "extra_fields"])
def test_ambiguous_or_noncontract_json_does_not_replace_the_old_card(client, provider, problem):
    raw = json.dumps(OLD)
    suffix = {
        "duplicate_json_key": ', "claims": []}',
        "nonstandard_json_constant": ', "debug": NaN}',
        "extra_fields": ', "debug": "extra"}',
    }
    provider.answers["memory"] = lambda body: ok(raw[:-1] + suffix[problem])
    response = client.post("/v1/memory", json=memory_request(OLD))
    assert response.status_code == 502 and response.json()["error"]["code"] == "invalid_output"
    assert len(provider.calls) == 1


def test_contact_detection_happens_before_length_truncation(client, provider):
    card = copy.deepcopy(OLD)
    card["name"] = "A" * 30 + " 010-1234-5678"
    card["claims"] = ["X" * 92 + " 010-1234-5678", "says tea is their favorite drink"]
    provider.answers["memory"] = lambda body: ok(json.dumps(card))
    response = client.post("/v1/memory", json=memory_request(OLD))
    assert response.status_code == 200
    result = response.json()["memory_card"]
    assert result["name"] == "" and result["claims"] == ["says tea is their favorite drink"]


def test_valid_empty_replacement_is_not_merged_with_the_old_card(client, provider):
    empty = {"name": "", "claims": [], "events": [], "attitude": "neutral", "struggled": []}
    provider.answers["memory"] = lambda body: ok(json.dumps(empty))
    response = client.post("/v1/memory", json=memory_request(OLD, "Please forget what I told you about myself."))
    assert response.status_code == 200 and response.json()["memory_card"] == empty


def test_session_cards_flow_into_next_visit_without_old_preference_restoration(client, provider):
    """저장 대역은 호출 측 역할이며 실제 Spring·DB 구현을 의미하지 않는다."""
    tea = OLD | {"claims": ["says tea is their favorite drink"]}
    forgotten = {"name": "", "claims": [], "events": [], "attitude": "neutral", "struggled": []}
    sessions = [
        ("My name is Alex. My favorite drink is coffee.", OLD),
        ("I now prefer tea.", tea),
        ("Please forget what I told you about myself.", forgotten),
    ]
    stored = None
    captured = {}
    current_target = None

    def memory_answer(body):
        assert json.dumps(stored or {}, ensure_ascii=False) in body["messages"][1]["content"]
        assert body["response_format"]["type"] == "json_schema"
        assert body["response_format"]["json_schema"]["strict"] is True
        assert set(body["response_format"]["json_schema"]["schema"]["required"]) == set(OLD) | {"impression"}
        return ok(json.dumps(current_target), usage=(90, 35))

    def npc_answer(body):
        captured["system"] = body["messages"][0]["content"]
        return stream(["Welcome back."])

    provider.answers.update(memory=memory_answer, npc=npc_answer)
    for visits, (text, target) in enumerate(sessions, 1):
        current_target = target
        response = client.post("/v1/memory", json=memory_request(stored, text))
        assert response.status_code == 200
        body = response.json()
        assert set(body) == {"memory_card", "impression", "affinity_delta", "usage"}
        assert body["memory_card"] == target
        assert body["usage"][0]["step"] == "memory" and body["usage"][0]["input_tokens"] == 90
        stored = copy.deepcopy(body["memory_card"])
        reply = client.post(
            "/v1/npc/reply",
            json={
                "npc": NPC,
                "text": "Hello again.",
                "history": [],
                "tts": False,
                "memory": {"visits": visits, "last_order": None, "card": stored},
            },
        )
        assert reply.status_code == 200 and "event: done" in reply.text
        if visits == 2:
            assert "tea" in captured["system"] and "coffee" not in captured["system"]
        elif visits == 3:
            assert "tea" not in captured["system"] and "Alex" not in captured["system"]
    assert stored == forgotten


@pytest.mark.parametrize(
    ("failure", "status", "code"),
    [
        ("bad_card", 502, "invalid_output"),
        ("unavailable", 503, "provider_unavailable"),
        ("limit", 429, "provider_limit"),
        ("timeout", 503, "provider_unavailable"),
    ],
)
def test_failed_update_leaves_callers_card_available_for_the_next_visit(client, provider, failure, status, code):
    stored = copy.deepcopy(OLD)

    def answer(body):
        if failure == "timeout":
            raise httpx.ReadTimeout("synthetic timeout")
        if failure == "bad_card":
            return ok('{"name": "Alex"}')
        return httpx.Response(429 if failure == "limit" else 503, json={"error": {"message": "synthetic provider failure"}})

    provider.answers["memory"] = answer
    response = client.post("/v1/memory", json=memory_request(stored))
    assert response.status_code == status and response.json()["error"]["code"] == code
    assert "memory_card" not in response.json()
    assert stored == OLD
    captured = {}

    def npc_answer(body):
        captured["system"] = body["messages"][0]["content"]
        return stream(["Welcome back."])

    provider.answers["npc"] = npc_answer
    reply = client.post("/v1/npc/reply", json={"npc": NPC, "text": "Hello.", "tts": False, "memory": {"visits": 1, "card": stored}})
    assert reply.status_code == 200 and "event: done" in reply.text
    assert "Alex" in captured["system"] and "coffee" in captured["system"]


def test_request_supplied_cards_do_not_leak_between_requests(client, provider):
    cards = [copy.deepcopy(OLD), OLD | {"name": "Bob", "claims": ["says tea is their favorite drink"]}]
    messages = []

    def answer(body):
        messages.append(body["messages"][1]["content"])
        return ok(json.dumps(cards[len(messages) - 1]))

    provider.answers["memory"] = answer
    for card in cards:
        response = client.post("/v1/memory", json=memory_request(card, "Hello."))
        assert response.status_code == 200 and response.json()["memory_card"] == card
    assert "Alex" in messages[0] and "Bob" not in messages[0]
    assert "Bob" in messages[1] and "Alex" not in messages[1]


def test_slow_memory_generation_is_not_duplicated_to_race_the_first_call(provider):
    async def answer(body):
        await asyncio.sleep(0.1)
        return ok(json.dumps(OLD))

    provider.answers["memory"] = answer
    with make_client(provider, hedge_after_s=0.01) as client:
        response = client.post("/v1/memory", json=memory_request())
    assert response.status_code == 200
    assert provider.calls == [("memory", "openai/gpt-oss-120b")]
    assert "hedged" not in response.json()["usage"][0]
