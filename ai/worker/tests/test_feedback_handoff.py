"""백엔드 전달 JSON을 실제 FastAPI 경로와 대조한다. 모델은 MockTransport다."""

import json
from pathlib import Path

import pytest

from conftest import ok

ROOT = Path(__file__).resolve().parents[3]
EXAMPLES = json.loads((ROOT / "docs/api/examples/feedback-contract.json").read_text(encoding="utf-8"))


@pytest.mark.parametrize("case", EXAMPLES["cases"], ids=lambda case: case["id"])
def test_backend_feedback_example_matches_worker_and_call_count(client, provider, case):
    captured = []
    raw = case["mock_content_override"]
    if raw is None:
        raw = (ROOT / "ai/feedback/samples" / case["mock_fixture"]).read_text(encoding="utf-8")

    def answer(body):
        captured.append(body["messages"])
        return ok(raw)

    provider.answers["feedback"] = answer
    response = client.post("/v1/feedback", json=case["request"])
    body = response.json()
    for usage in body.get("usage", []):
        assert usage["latency_s"] >= 0
        usage["latency_s"] = 0.0
    assert response.status_code == case["response_http_status"]
    assert body == case["response"]
    assert len(provider.calls) == case["expected_model_calls"]
    assert all(step == "feedback" for step, _ in provider.calls)
    assert "SYNTHETIC_BLOCKED_MARKER" not in json.dumps(captured)
