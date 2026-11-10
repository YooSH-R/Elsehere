"""의도 판정(/v1/judge)의 GPU 서버 모델 → 구성의 의도 모델. 무례 판정(/v1/guard)과 같은 방식이다:
JUDGE_GPU_MODEL이 있으면 GPU 서버 vLLM을 먼저 한 번 부르고, 실패·시간 초과·JSON이 아닌 답이면 구성의 intent 모델(GMS Luna)로 넘긴다.
GPU 서버 사용 허용이 확정되지 않아 GPU가 끊겨도 판정이 이어져야 한다. 가짜 공급자라 진짜 호출 없음."""

import asyncio
import json

import httpx
import pytest
from test_api import judge_body

from app import config
from app.config import Settings, profile_models
from conftest import TOKEN, make_client, ok

GPU_MODEL = "Qwen/Qwen3.8-27B"
GPU = {"judge_gpu_model": GPU_MODEL, "providers": {"gpu_llm": {"base_url": "http://gpu/v1", "api_key": "g"}}, "judge_gpu_timeout_s": 0.2}
PROFILE_INTENT = profile_models("groq")[0]["intent"]
ORDER = {"intents": ["order_items"], "items": [{"name": "latte", "iced": False, "qty": 1}], "choice": "none", "events": []}


def intent_by_model(answers: dict):
    """의도 판정 요청을 모델 이름으로 나눠 답한다 (GPU 서버 모델 / 구성의 의도 모델)."""

    async def answer(b):
        res = answers[b["model"]](b)
        return await res if asyncio.iscoroutine(res) else res

    return answer


def judge(provider, text="One latte, please.", **settings) -> dict:
    with make_client(provider, **{**GPU, **settings}) as c:
        r = c.post("/v1/judge", json=judge_body(text))
    assert r.status_code == 200, r.text
    return r.json()


def test_gpu_intent_model_answers_first(provider):
    provider.answers["intent"] = intent_by_model({GPU_MODEL: lambda b: ok(json.dumps(ORDER))})

    body = judge(provider)

    assert body["intent"]["id"] == "order_items" and body["intent"]["error"] is None
    assert provider.calls == [("intent", GPU_MODEL)]
    assert [(u["step"], u["model"], u["fallback"]) for u in body["usage"]] == [("intent", GPU_MODEL, False)]


def test_gpu_intent_request_keeps_the_output_format_and_turns_thinking_off(provider):
    seen = {}
    provider.answers["intent"] = intent_by_model({GPU_MODEL: lambda b: seen.update(b) or ok(json.dumps(ORDER))})

    judge(provider)

    assert seen["chat_template_kwargs"] == {"enable_thinking": False}
    assert seen["response_format"]["type"] == "json_schema"
    assert "events" in seen["response_format"]["json_schema"]["schema"]["required"]
    assert seen["max_tokens"] == 200 and seen["temperature"] == 0.0


@pytest.mark.parametrize(
    "gpu_answer",
    [lambda b: httpx.Response(503, text="down"), lambda b: ok("not json"), lambda b: ok("")],
    ids=["server-error", "not-json", "empty"],
)
def test_gpu_intent_failure_falls_back_to_profile_intent(provider, gpu_answer):
    provider.answers["intent"] = intent_by_model({GPU_MODEL: gpu_answer, PROFILE_INTENT: lambda b: ok(json.dumps(ORDER))})

    body = judge(provider)

    assert body["intent"]["id"] == "order_items" and body["intent"]["error"] is None
    assert provider.calls == [("intent", GPU_MODEL), ("intent", PROFILE_INTENT)]  # GPU는 재시도하지 않고 한 번만
    assert body["usage"][-1]["model"] == PROFILE_INTENT and body["usage"][-1]["fallback"] is True


def test_slow_gpu_intent_falls_back_within_budget(provider):
    async def slow(b):
        await asyncio.sleep(0.5)
        return ok(json.dumps(ORDER))

    provider.answers["intent"] = intent_by_model({GPU_MODEL: slow, PROFILE_INTENT: lambda b: ok(json.dumps(ORDER))})

    body = judge(provider, judge_budget_s=2.0)

    assert body["intent"]["id"] == "order_items"
    assert [(u["model"], u["fallback"]) for u in body["usage"]] == [(PROFILE_INTENT, True)]


def test_gpu_intent_is_skipped_after_repeated_failures(provider):
    provider.answers["intent"] = intent_by_model(
        {GPU_MODEL: lambda b: httpx.Response(503, text="down"), PROFILE_INTENT: lambda b: ok(json.dumps(ORDER))}
    )
    with make_client(provider, **GPU, judge_breaker_failures=2, judge_breaker_skip_s=60.0) as c:
        for _ in range(3):
            c.post("/v1/judge", json=judge_body("One latte, please."))
    assert [model for _, model in provider.calls] == [GPU_MODEL, PROFILE_INTENT, GPU_MODEL, PROFILE_INTENT, PROFILE_INTENT]


def test_without_gpu_intent_model_the_profile_intent_judges(provider):
    provider.answers["intent"] = intent_by_model({PROFILE_INTENT: lambda b: ok(json.dumps(ORDER))})

    body = judge(provider, judge_gpu_model="")

    assert provider.calls == [("intent", PROFILE_INTENT)]
    assert body["usage"][0]["fallback"] is False


def test_gpu_intent_timeout_leaves_room_for_the_profile_intent(monkeypatch):
    """GPU 서버가 늦으면 끊고 Luna로 넘긴다. Luna 의도 판정(dev 중앙 1.4초·p90 1.8초)이 JUDGE_BUDGET_S 안에 들어와야 한다."""
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q"}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    s = Settings.from_env()
    assert s.judge_gpu_model == "" and s.judge_gpu_timeout_s == 2.0
    assert s.judge_budget_s - s.judge_gpu_timeout_s >= 2.5


def test_gpu_intent_from_env(monkeypatch):
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q", "JUDGE_GPU_MODEL": GPU_MODEL}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    with pytest.raises(RuntimeError, match="GUARD_GPU_BASE_URL"):  # 무례 판정과 같은 GPU 서버 vLLM 주소를 쓴다
        Settings.from_env()
    env |= {"GUARD_GPU_BASE_URL": "http://172.19.0.1:18432/v1", "GUARD_GPU_TOKEN": "t", "JUDGE_GPU_TIMEOUT_S": "1.5"}
    s = Settings.from_env()
    assert s.judge_gpu_model == GPU_MODEL and s.judge_gpu_timeout_s == 1.5 and s.guard_gpu_model == ""
    assert s.providers["gpu_llm"] == {"base_url": "http://172.19.0.1:18432/v1", "api_key": "t"}
    env |= {"JUDGE_BREAKER_FAILURES": "5", "JUDGE_BREAKER_SKIP_S": "30"}
    s = Settings.from_env()
    assert (s.judge_breaker_failures, s.judge_breaker_skip_s) == (5, 30.0)
