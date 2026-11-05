"""무례 판정 /v1/guard 테스트. 판정 모델은 가짜 공급자가 yes/no로 답한다."""

import asyncio

import httpx
import pytest
from test_npc_reply_profile import profile_of

from app import config
from app import npc_relationship as rel
from app.config import Settings, profile_models
from app.guard import guard_result
from conftest import TOKEN, make_client, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista at Maple Cafe.", "facts": "Menu: latte $5.", "scene": "Inside Maple Cafe."}
HISTORY = [{"who": "npc", "text": "Hi! What can I get for you?"}]


def guard_body(text, history=HISTORY):
    return {"npc": NPC, "history": history, "text": text}


def test_rude_line_is_judged_by_guard_model_only(client, provider):
    provider.answers["guard"] = lambda b: ok("yes")
    r = client.post("/v1/guard", json=guard_body("you idiot, hurry up"))
    assert r.status_code == 200
    body = r.json()
    assert body["text"] == "you idiot, hurry up" and body["dropped"] is False
    assert body["guard"] == {"abusive": True, "error": None, "error_code": None}
    assert [u["step"] for u in body["usage"]] == ["guard"]
    assert [step for step, _ in provider.calls] == ["guard"]  # 의도·번역은 부르지 않는다


def test_action_only_line_is_dropped_without_model_call(client, provider):
    r = client.post("/v1/guard", json=guard_body("*총을 꺼낸다*")).json()
    assert r == {"text": "", "dropped": True, "guard": None, "affinity_delta": None, "usage": []}
    assert provider.calls == []


def with_neuroticism(profile: dict, value: float) -> dict:
    personality = profile["personality"]
    return {**profile, "personality": {**personality, "dimensions": {**personality["dimensions"], "neuroticism": value}}}


def test_a_rude_line_comes_with_the_affinity_to_take_off_right_away(client, provider):
    """백엔드는 무례 발화를 저장하는 트랜잭션에서 affinity_delta를 바로 더한다. 인격 카드가 없으면 기본 배수다."""
    provider.answers["guard"] = lambda b: ok("yes")
    r = client.post("/v1/guard", json=guard_body("you idiot, hurry up")).json()
    assert r["affinity_delta"] == rel.rude_affinity_delta({}, rel.load_world_rules())
    assert r["affinity_delta"] < 0


def test_a_touchy_npc_takes_off_more_affinity_for_the_same_rude_line(client, provider):
    """요청의 인격 카드(성격 5축·행동 조각)로 계산한 반응 크기를 쓴다. 신경성이 높을수록 더 깎는다."""
    provider.answers["guard"] = lambda b: ok("yes")
    jake = profile_of("HOTEL_STAFF")

    def delta(neuroticism: float) -> int:
        npc = {**NPC, "profile": with_neuroticism(jake, neuroticism)}
        return client.post("/v1/guard", json={**guard_body("you idiot"), "npc": npc}).json()["affinity_delta"]

    assert delta(1.0) < delta(0.0) < 0


def test_no_affinity_change_unless_the_line_is_judged_rude(client, provider):
    """무례가 아니거나 판정하지 못했으면(abusive: null) 감점하지 않는다. 백엔드는 이때 기본값도 쓰지 않는다."""
    provider.answers["guard"] = lambda b: ok("no")
    assert client.post("/v1/guard", json=guard_body("hello")).json()["affinity_delta"] is None
    provider.answers["guard"] = lambda b: httpx.Response(503, text="down")
    assert client.post("/v1/guard", json=guard_body("hello")).json()["affinity_delta"] is None


def test_blank_text_is_invalid_request(client, provider):
    r = client.post("/v1/guard", json=guard_body("   "))
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"
    assert provider.calls == []


def test_slow_guard_hits_guard_budget(provider):
    """판정이 GUARD_BUDGET_S를 넘기면 미판정(timeout)으로 200. 의도·번역의 상한(JUDGE_BUDGET_S)과는 따로다."""

    async def slow(b):
        await asyncio.sleep(0.5)
        return ok("yes")

    provider.answers["guard"] = slow
    with make_client(provider, guard_budget_s=0.2, judge_budget_s=5.0) as c:
        r = c.post("/v1/guard", json=guard_body("hello"))
    assert r.status_code == 200
    assert r.json()["guard"]["abusive"] is None and r.json()["guard"]["error_code"] == "timeout"


def test_guard_budget_from_env(monkeypatch):
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q"}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    assert Settings.from_env().guard_budget_s == 5.0
    env["GUARD_BUDGET_S"] = "2.5"
    assert Settings.from_env().guard_budget_s == 2.5


def test_unclear_answer_is_not_counted_as_abusive(client, provider):
    """빈 답·거절은 미판정 (예전에는 무례로 셌다). Spring은 무례로 세지 않고 번역 카드도 보여 주지 않는다."""
    provider.answers["guard"] = lambda b: ok("")
    r = client.post("/v1/guard", json=guard_body("커피 줘")).json()
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "empty"


def test_provider_failure_is_unjudged(client, provider):
    provider.answers["guard"] = lambda b: httpx.Response(503, text="down")
    r = client.post("/v1/guard", json=guard_body("hello"))
    assert r.status_code == 200
    assert r.json()["guard"]["abusive"] is None and r.json()["guard"]["error_code"] == "provider_unavailable"


def test_guard_reads_only_clear_yes_no():
    assert guard_result("Yes.")["abusive"] is True and guard_result(" no, it's polite")["abusive"] is False
    assert guard_result("") == {"abusive": None, "error": "판정 모델이 빈 답을 냄", "error_code": "empty"}
    for out in ("I can't help with that.", "nothing rude", "yesterday", "Maybe"):  # 거절·다른 말은 무례도 정상도 아님
        assert guard_result(out)["abusive"] is None and guard_result(out)["error_code"] == "unparsed", out


# ---------- GPU 서버 판정 모델 → 구성의 판정 모델 ----------
GPU_MODEL = "guard-qwen3.8-27b-r16"
GPU = {"guard_gpu_model": GPU_MODEL, "providers": {"gpu_llm": {"base_url": "http://gpu/v1", "api_key": "g"}}, "guard_gpu_timeout_s": 0.2}


def guard_by_model(answers: dict):
    """판정 요청을 모델 이름으로 나눠 답한다 (GPU 서버 모델 / 구성의 판정 모델)."""

    async def answer(b):
        res = answers[b["model"]](b)
        return await res if asyncio.iscoroutine(res) else res

    return answer


def test_gpu_guard_model_answers_first(provider):
    provider.answers["guard"] = guard_by_model({GPU_MODEL: lambda b: ok("yes")})
    with make_client(provider, **GPU) as c:
        body = c.post("/v1/guard", json=guard_body("you idiot")).json()
    assert body["guard"]["abusive"] is True
    assert provider.calls == [("guard", GPU_MODEL)]
    assert [(u["model"], u["fallback"]) for u in body["usage"]] == [(GPU_MODEL, False)]


@pytest.mark.parametrize("text", ["음... 맛있군 좀 살 것 같ㅊ아", "ㄱㅅ", "ㅅㅂ 맛없네"])
def test_line_with_loose_korean_letters_skips_gpu_guard(provider, text):
    """GPU LoRA는 오타 낱자모("같ㅊ아")·"ㄱㅅ"를 욕 줄임말로 읽어 무례로 본다(프롬프트로 안 고쳐짐, S15P31A305-604).
    낱자모가 섞인 줄은 구성의 guard 모델이 판정한다."""
    profile_guard = profile_models("groq")[0]["guard"]
    provider.answers["guard"] = guard_by_model({GPU_MODEL: lambda b: ok("yes"), profile_guard: lambda b: ok("no")})
    with make_client(provider, **GPU) as c:
        body = c.post("/v1/guard", json=guard_body(text)).json()
    assert provider.calls == [("guard", profile_guard)]
    assert [(u["model"], u["fallback"]) for u in body["usage"]] == [(profile_guard, False)]


def test_line_with_full_korean_syllables_still_uses_gpu_guard(provider):
    provider.answers["guard"] = guard_by_model({GPU_MODEL: lambda b: ok("no")})
    with make_client(provider, **GPU) as c:
        c.post("/v1/guard", json=guard_body("음... 맛있군 좀 살 것 같아"))
    assert provider.calls == [("guard", GPU_MODEL)]


def test_gpu_guard_request_turns_thinking_off(provider):
    seen = {}
    provider.answers["guard"] = guard_by_model({GPU_MODEL: lambda b: seen.update(b) or ok("no")})
    with make_client(provider, **GPU) as c:
        c.post("/v1/guard", json=guard_body("hello"))
    assert seen["chat_template_kwargs"] == {"enable_thinking": False} and seen["max_tokens"] == 5


def test_gpu_guard_failure_falls_back_to_profile_guard(provider):
    profile_guard = profile_models("groq")[0]["guard"]
    provider.answers["guard"] = guard_by_model({GPU_MODEL: lambda b: httpx.Response(503, text="down"), profile_guard: lambda b: ok("no")})
    with make_client(provider, **GPU) as c:
        body = c.post("/v1/guard", json=guard_body("hello")).json()
    assert body["guard"]["abusive"] is False
    assert provider.calls == [("guard", GPU_MODEL), ("guard", profile_guard)]  # GPU는 재시도하지 않고 한 번만
    assert [(u["model"], u["fallback"]) for u in body["usage"]] == [(profile_guard, True)]


def test_slow_gpu_guard_falls_back_within_budget(provider):
    async def slow(b):
        await asyncio.sleep(0.5)
        return ok("yes")

    profile_guard = profile_models("groq")[0]["guard"]
    provider.answers["guard"] = guard_by_model({GPU_MODEL: slow, profile_guard: lambda b: ok("no")})
    with make_client(provider, **GPU) as c:
        body = c.post("/v1/guard", json=guard_body("hello")).json()
    assert body["guard"]["abusive"] is False and body["usage"][0]["model"] == profile_guard


def test_gpu_guard_is_skipped_after_repeated_failures(provider):
    profile_guard = profile_models("groq")[0]["guard"]
    provider.answers["guard"] = guard_by_model({GPU_MODEL: lambda b: httpx.Response(503, text="down"), profile_guard: lambda b: ok("no")})
    with make_client(provider, **GPU, guard_breaker_failures=2, guard_breaker_skip_s=60.0) as c:
        for _ in range(3):
            c.post("/v1/guard", json=guard_body("hello"))
    assert [model for _, model in provider.calls] == [GPU_MODEL, profile_guard, GPU_MODEL, profile_guard, profile_guard]


def test_gpu_guard_waits_past_vllm_first_batch_but_leaves_room_for_fallback(monkeypatch):
    """vLLM은 처음 보는 동시 요청 수에서 한 번 2.4~2.7초가 걸린다(평소 10개 동시 약 1초). 2초에 끊으면 dev 5명 첫 턴이
    Luna 재호출로 6~9초가 됐다 (S15P31A305-511). 넘겨도 Luna 판정(약 1~1.5초)이 GUARD_BUDGET_S 안에 들어와야 한다."""
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q"}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    s = Settings.from_env()
    assert s.guard_gpu_timeout_s == 3.0
    assert s.guard_budget_s - s.guard_gpu_timeout_s >= 1.5


def test_gpu_guard_from_env(monkeypatch):
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q", "GUARD_GPU_MODEL": GPU_MODEL}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    with pytest.raises(RuntimeError, match="GUARD_GPU_BASE_URL"):
        Settings.from_env()
    env |= {"GUARD_GPU_BASE_URL": "http://172.19.0.1:18432/v1", "GUARD_GPU_TOKEN": "t", "GUARD_GPU_TIMEOUT_S": "1.5"}
    s = Settings.from_env()
    assert s.guard_gpu_model == GPU_MODEL and s.guard_gpu_timeout_s == 1.5
    assert s.providers["gpu_llm"] == {"base_url": "http://172.19.0.1:18432/v1", "api_key": "t"}
    env.pop("GUARD_GPU_MODEL")
    assert Settings.from_env().guard_gpu_model == "" and "gpu_llm" not in Settings.from_env().providers
