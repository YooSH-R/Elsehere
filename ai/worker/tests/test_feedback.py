"""대화 종료 피드백(/v1/feedback): ai/feedback 모듈(계약 0.3.0)을 워커의 모델 구성으로 부른다.
입력·기대 출력은 그 모듈의 예시(ai/feedback/samples)를 그대로 쓴다. 가짜 공급자라 진짜 호출 없음."""

import asyncio
import copy
import json
from pathlib import Path

import httpx
from jsonschema import Draft202012Validator

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


def test_compact_provider_response_is_valid_and_restored_before_backend_receives_it(client, provider):
    payload = sample("inputs/free_grammar.json")
    raw = sample("fixtures/free_grammar.llm.json")
    raw["format_version"] = "compact-v1"
    raw["spoken_usage_selection"] = None
    result = raw["result"]
    review = result["turn_reviews"][0]
    for name in ("review_id", "original_text", "assisted"):
        del review[name]
    del review["suggestions"][0]["suggestion_id"]
    result["summary_suggestion_id"] = review["turn_id"]
    for tag in result["review_tags"]:
        del tag["tag_id"], tag["assisted"]
    for candidate in result["vocab_candidates"]:
        del candidate["candidate_id"]
        candidate["source"].update(review_id=review["turn_id"], suggestion_id=review["turn_id"])
    result["situation_expressions"] = [
        {
            "expression": "How do I get there?",
            "meaning_ko": "어떻게 가나요?",
            "usage_ko": "가는 방법을 물을 때 써요.",
            "example_en": "How do I get there from here?",
            "example_ko": "여기서 어떻게 가나요?",
        },
        {
            "expression": "Is it far from here?",
            "meaning_ko": "여기서 먼가요?",
            "usage_ko": "거리를 물을 때 써요.",
            "example_en": "Is the station far from here?",
            "example_ko": "역이 여기서 먼가요?",
        },
    ]

    def answer(body):
        provider_response = copy.deepcopy(raw)
        for expression in provider_response["result"]["situation_expressions"]:
            expression["speaker_role"] = "learner"
        Draft202012Validator(body["response_format"]["json_schema"]["schema"]).validate(provider_response)
        return ok(json.dumps(provider_response, ensure_ascii=False))

    provider.answers["feedback"] = answer
    response = client.post("/v1/feedback", json=payload)
    assert response.status_code == 200
    feedback = response.json()["feedback"]
    assert feedback["status"] == "completed"
    restored = feedback["result"]["turn_reviews"][0]
    assert restored["original_text"] == payload["turns"][1]["final_text"]
    assert restored["review_id"] == restored["suggestions"][0]["suggestion_id"] == restored["turn_id"]
    assert restored["assisted"] is False
    assert feedback["result"]["situation_expressions"] == result["situation_expressions"]
    assert "format_version" not in feedback
    assert len(provider.calls) == 1


def test_feedback_without_user_turns_does_not_call_model(client, provider):
    r = client.post("/v1/feedback", json=sample("inputs/no_user_turns.json"))
    assert r.status_code == 200 and r.json()["feedback"]["status"] == "insufficient_evidence"
    assert provider.calls == [] and r.json()["usage"] == []


def test_feedback_blocked_turns_do_not_create_a_model_call(client, provider):
    payload = sample("inputs/free_grammar.json")
    payload["turns"][1]["text_status"] = "blocked"
    response = client.post("/v1/feedback", json=payload)
    assert response.status_code == 200
    assert response.json()["feedback"]["status"] == "insufficient_evidence"
    assert response.json()["usage"] == []
    assert provider.calls == []


def test_feedback_mixed_history_reviews_only_final_user_turns(client, provider):
    seen = {}

    def answer(body):
        seen["prompt"] = body["messages"][1]["content"]
        return ok(json.dumps(sample("fixtures/free_grammar.llm.json"), ensure_ascii=False))

    provider.answers["feedback"] = answer
    payload = sample("inputs/free_grammar.json")
    blocked = copy.deepcopy(payload["turns"][1])
    blocked.update(turn_id="t04", sequence=4, text_status="blocked", final_text="BLOCKED_MARKER")
    payload["turns"].append(blocked)
    response = client.post("/v1/feedback", json=payload)
    assert response.status_code == 200
    result = response.json()["feedback"]
    assert result["schema_version"] == "0.3.0" and result["status"] == "completed"
    assert [review["turn_id"] for review in result["result"]["turn_reviews"]] == ["t02"]
    assert "BLOCKED_MARKER" not in seen["prompt"]
    assert [step for step, _ in provider.calls] == ["feedback"]


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


# ---------------------------------------------------------------------
# 코드 리뷰 #5·#6: 피드백 상한은 전체 시간이고, 피드백은 자기 스레드에서 돈다
# ---------------------------------------------------------------------
def test_feedback_timeout_is_whole_call_and_reported_as_ai_timeout(provider):
    """재시도·대체 모델을 거쳐도 FEEDBACK_TIMEOUT_S 안에 끝내고, 시간 초과는 AI_TIMEOUT."""
    import time

    from conftest import make_client

    async def stuck(b):
        await asyncio.sleep(2.0)
        return ok("{}")

    provider.answers["feedback"] = stuck
    with make_client(provider, feedback_timeout_s=0.3) as c:
        t = time.perf_counter()
        r = c.post("/v1/feedback", json=sample("inputs/free_grammar.json"))
        spent = time.perf_counter() - t
    fb = r.json()["feedback"]
    assert fb["status"] == "failed" and fb["problem"]["code"] == "AI_TIMEOUT"
    assert spent < 1.2


def test_feedback_runs_in_its_own_threads(client, monkeypatch):
    """피드백(길게 걸림)이 음성 합성·로컬 판정기가 쓰는 기본 스레드 묶음을 차지하지 않는다."""
    import threading

    from app import feedback_bridge

    seen = {}

    def fake_generate(payload, model, *, ai_allowed):
        seen["thread"] = threading.current_thread().name
        return {"status": "completed"}

    monkeypatch.setattr(feedback_bridge, "generate_feedback", fake_generate)
    r = client.post("/v1/feedback", json=sample("inputs/free_grammar.json"))
    assert r.status_code == 200 and seen["thread"].startswith("feedback")
