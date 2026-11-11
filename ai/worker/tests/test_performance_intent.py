import asyncio
import json
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from app.performance_intent import PerformanceIntentRequest, classify
from conftest import FakeTts, make_client, ok


def test_classifier_does_not_race_duplicate_model_calls_when_slow():
    llm = SimpleNamespace(s=SimpleNamespace(judge_budget_s=5), chat=AsyncMock(return_value='{"intent":"REPLAY_RAP"}'))
    asyncio.run(classify(llm, PerformanceIntentRequest(text="An encore?")))
    assert llm.chat.await_count == 1
    assert llm.chat.call_args.kwargs.get("hedge") is False


@pytest.mark.parametrize("intent", ["REPLAY_RAP", "CONVERSATION"])
def test_performance_intent_only_classifies_without_reply_or_speech(provider, intent):
    tts = FakeTts()
    seen = []

    def classify(body):
        seen.append(body)
        return ok(json.dumps({"intent": intent}))

    provider.answers["intent"] = classify
    with make_client(provider, tts=tts) as client:
        response = client.post("/v1/performance/intent", json={"text": "I'd love an encore of that burger beat."})
    assert response.status_code == 200
    assert response.json()["intent"] == intent
    assert [item["step"] for item in response.json()["usage"]] == ["performance_intent"]
    assert len(seen) == 1
    assert not tts.texts


@pytest.mark.parametrize("result", ['{"intent":"SING"}', '{"intent":null}', "{}"])
def test_invalid_classification_does_not_generate_a_reply(client, provider, result):
    provider.answers["intent"] = lambda body: ok(result)
    response = client.post("/v1/performance/intent", json={"text": "That beat again?"})
    assert response.status_code == 502


@pytest.mark.parametrize("text", ["", " ", "x" * 1001])
def test_performance_input_is_bounded(client, text):
    assert client.post("/v1/performance/intent", json={"text": text}).status_code == 400
