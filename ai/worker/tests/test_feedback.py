"""대화 종료 피드백(/v1/feedback): ai/feedback 모듈(계약 0.3.0)을 워커의 모델 구성으로 부른다.
입력·기대 출력은 그 모듈의 예시(ai/feedback/samples)를 그대로 쓴다. 가짜 공급자라 진짜 호출 없음."""

import asyncio
import json
from pathlib import Path

import httpx

from conftest import ok

SAMPLES = Path(__file__).resolve().parents[2] / "feedback" / "samples"


def sample(name: str) -> dict:
    return json.loads((SAMPLES / name).read_text(encoding="utf-8"))


def test_feedback_follows_ai_feedback_contract(client, provider):
    seen = {}

    def answer(b):
        seen["body"] = b
        return ok(json.dumps(sample("fixtures/free_grammar.llm.json"), ensure_ascii=False))

    provider.answers["feedback"] = answer
    payload = sample("inputs/free_grammar.json")
    r = client.post("/v1/feedback", json=payload)
    assert r.status_code == 200
    fb = r.json()["feedback"]
    assert fb["status"] == "completed" and fb["schema_version"] == "0.3.0" and fb["request_id"] == payload["request_id"]
    assert fb["result"]["turn_reviews"] and fb["result"]["conversation_summary"]["text_ko"]
    assert [u["step"] for u in r.json()["usage"]] == ["feedback"]
    # 모듈이 정한 출력 형식(strict json_schema)과 출력 상한으로 부른다
    assert seen["body"]["response_format"]["type"] == "json_schema" and seen["body"]["response_format"]["json_schema"]["strict"]
    assert seen["body"]["max_tokens"] == 6000


def test_feedback_without_user_turns_does_not_call_model(client, provider):
    r = client.post("/v1/feedback", json=sample("inputs/no_user_turns.json"))
    assert r.status_code == 200 and r.json()["feedback"]["status"] == "insufficient_evidence"
    assert provider.calls == [] and r.json()["usage"] == []


def test_feedback_provider_failure_is_failed_status(client, provider):
    provider.answers["feedback"] = lambda b: httpx.Response(503, text="down")
    r = client.post("/v1/feedback", json=sample("inputs/free_grammar.json"))
    fb = r.json()["feedback"]
    assert r.status_code == 200 and fb["status"] == "failed" and fb["problem"]["code"] == "AI_PROVIDER_ERROR"


def test_feedback_broken_output_is_invalid_model_output(client, provider):
    provider.answers["feedback"] = lambda b: ok('{"result": "nope"}')
    r = client.post("/v1/feedback", json=sample("inputs/free_grammar.json"))
    assert r.status_code == 200 and r.json()["feedback"]["problem"]["code"] == "INVALID_MODEL_OUTPUT"


def test_feedback_bad_input_is_invalid_request(client):
    payload = sample("inputs/free_grammar.json") | {"schema_version": "0.2.0"}
    r = client.post("/v1/feedback", json=payload)
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"


def test_feedback_is_not_hedged_or_cut_at_short_timeout(provider):
    """피드백은 길게 생성하므로 짧은 시간 상한·같은 요청 더 보내기를 쓰지 않는다 (feedback_timeout_s)."""
    from conftest import make_client

    async def slow(b):
        await asyncio.sleep(0.4)
        return ok(json.dumps(sample("fixtures/free_grammar.llm.json"), ensure_ascii=False))

    provider.answers["feedback"] = slow
    with make_client(provider, call_timeout_s=0.2, hedge_after_s=0.1, feedback_timeout_s=2.0) as c:
        r = c.post("/v1/feedback", json=sample("inputs/free_grammar.json"))
    assert r.json()["feedback"]["status"] == "completed"
    assert len([x for x in provider.calls if x[0] == "feedback"]) == 1
