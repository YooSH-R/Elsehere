"""NPC 대답(/v1/npc/reply)의 GPU 서버 모델 → 구성의 npc 모델. NPC_GPU_MODEL이 있으면 GPU 서버 vLLM(NPC 대답 LoRA)에
스트림을 먼저 열고, 실패하거나 첫 글자가 npc_gpu_first_token_s 안에 안 오거나 GPU로 가는 대답이 이미 npc_gpu_max_concurrent개면
구성의 npc 모델(GMS SOL)로 대답한다. 첫 글자를 보낸 뒤에는 바꾸지 않는다. 가짜 공급자라 진짜 호출 없음."""

import asyncio
import json

import httpx
import pytest
from test_stream_hedge import BODY, SlowBody, events, npc_usage

from app import config
from app.config import Settings, profile_models
from app.llm import LLM, Usage
from conftest import TOKEN, make_client, stream

GPU_MODEL = "npc-qwen3.8-27b-r16"
GPU = {
    "npc_gpu_model": GPU_MODEL,
    "providers": {"gpu_llm": {"base_url": "http://gpu/v1", "api_key": "g"}},
    "npc_gpu_first_token_s": 0.3,
    "stream_hedge_after_s": 0,
}
PROFILE_NPC = profile_models("groq")[0]["npc"]


def by_model(answers: dict):
    """NPC 대답 요청을 모델 이름으로 나눠 답한다 (GPU 서버 모델 / 구성의 npc 모델)."""

    async def answer(b):
        res = answers[b["model"]](b)
        return await res if asyncio.iscoroutine(res) else res

    return answer


def reply(provider, **settings):
    with make_client(provider, **{**GPU, **settings}) as c:
        return events(c.post("/v1/npc/reply", json=BODY))


def test_gpu_npc_model_answers_first(provider):
    seen = {}
    provider.answers["npc"] = by_model({GPU_MODEL: lambda b: seen.update(b) or stream(["From ", "GPU."])})

    ev = reply(provider)

    assert ev[-1][0] == "done" and ev[-1][1]["text"] == "From GPU."
    assert provider.calls == [("npc", GPU_MODEL)]
    assert (npc_usage(ev)["model"], npc_usage(ev)["fallback"]) == (GPU_MODEL, False)
    assert seen["chat_template_kwargs"] == {"enable_thinking": False}  # 학습할 때처럼 생각 없이
    assert seen["stream"] is True


@pytest.mark.parametrize(
    "gpu_answer",
    [lambda b: httpx.Response(503, text="down"), lambda b: httpx.Response(200, content=b"data: [DONE]\n\n")],
    ids=["server-error", "no-text"],
)
def test_gpu_npc_failure_falls_back_to_profile_npc(provider, gpu_answer):
    provider.answers["npc"] = by_model({GPU_MODEL: gpu_answer, PROFILE_NPC: lambda b: stream(["From profile."])})

    ev = reply(provider)

    assert ev[-1][1]["text"] == "From profile."
    assert provider.calls == [("npc", GPU_MODEL), ("npc", PROFILE_NPC)]  # GPU는 재시도하지 않고 한 번만
    assert (npc_usage(ev)["model"], npc_usage(ev)["fallback"]) == (PROFILE_NPC, True)


def test_slow_gpu_first_text_falls_back_and_closes_the_gpu_stream(provider):
    closed = []
    provider.answers["npc"] = by_model(
        {
            GPU_MODEL: lambda b: httpx.Response(200, stream=SlowBody(1.0, ["Too late."], closed)),
            PROFILE_NPC: lambda b: stream(["From profile."]),
        }
    )

    ev = reply(provider)

    assert ev[-1][1]["text"] == "From profile."
    assert "closed" in closed


def test_gpu_npc_is_skipped_after_repeated_failures(provider):
    provider.answers["npc"] = by_model(
        {GPU_MODEL: lambda b: httpx.Response(503, text="down"), PROFILE_NPC: lambda b: stream(["From profile."])}
    )
    with make_client(provider, **GPU, npc_breaker_failures=2, npc_breaker_skip_s=60.0) as c:
        for _ in range(3):
            c.post("/v1/npc/reply", json=BODY)
    assert [model for _, model in provider.calls] == [GPU_MODEL, PROFILE_NPC, GPU_MODEL, PROFILE_NPC, PROFILE_NPC]


def test_without_gpu_npc_model_the_profile_npc_answers(provider):
    provider.answers["npc"] = by_model({PROFILE_NPC: lambda b: stream(["From profile."])})

    ev = reply(provider, npc_gpu_model="")

    assert provider.calls == [("npc", PROFILE_NPC)]
    assert npc_usage(ev)["fallback"] is False


async def _two_replies_at_once(provider) -> list[str]:
    gate = asyncio.Event()

    async def gpu(b):
        await gate.wait()
        return stream(["From GPU."])

    async def profile(b):
        gate.set()  # 둘째 대답이 구성의 모델로 간 뒤에 첫째(GPU)가 끝난다
        return stream(["From profile."])

    provider.answers["npc"] = by_model({GPU_MODEL: gpu, PROFILE_NPC: profile})
    s = Settings(
        token=TOKEN,
        base_url="http://fake/v1",
        api_key="x",
        profile="groq",
        models=profile_models("groq")[0],
        role_provider={},
        stt_fallback={},
        **{**GPU, "npc_gpu_first_token_s": 2.0, "npc_gpu_max_concurrent": 1},
    )
    llm = LLM(s, httpx.MockTransport(provider.handler))
    msgs = [{"role": "system", "content": "You are Mia."}, {"role": "user", "content": "Hi"}]

    async def one():
        return "".join([p async for p in llm.npc_stream(msgs, Usage(), max_tokens=50)])

    first = asyncio.create_task(one())
    await asyncio.sleep(0.05)  # 첫째가 GPU 자리를 잡을 때까지
    return [await one(), await first]


def test_reply_over_the_gpu_limit_goes_straight_to_profile_npc(provider):
    second, first = asyncio.run(_two_replies_at_once(provider))

    assert (first, second) == ("From GPU.", "From profile.")
    assert provider.calls == [("npc", GPU_MODEL), ("npc", PROFILE_NPC)]


def test_gpu_npc_from_env(monkeypatch):
    env = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q", "NPC_GPU_MODEL": GPU_MODEL}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", env)
    with pytest.raises(RuntimeError, match="GUARD_GPU_BASE_URL"):  # 무례 판정과 같은 GPU 서버 vLLM 주소를 쓴다
        Settings.from_env()
    env |= {"GUARD_GPU_BASE_URL": "http://172.19.0.1:18432/v1", "GUARD_GPU_TOKEN": "t"}
    s = Settings.from_env()
    assert s.npc_gpu_model == GPU_MODEL and s.providers["gpu_llm"]["api_key"] == "t"
    assert (s.npc_gpu_first_token_s, s.npc_gpu_max_concurrent) == (
        1.5,
        4,
    )  # 처음 보는 프롬프트도 첫 글자 0.8~1.0초라 SOL(약 1.5초)과 같은 상한
    env |= {"NPC_GPU_FIRST_TOKEN_S": "0.8", "NPC_GPU_MAX_CONCURRENT": "6", "NPC_BREAKER_FAILURES": "5", "NPC_BREAKER_SKIP_S": "30"}
    s = Settings.from_env()
    assert (s.npc_gpu_first_token_s, s.npc_gpu_max_concurrent, s.npc_breaker_failures, s.npc_breaker_skip_s) == (0.8, 6, 5, 30.0)


def test_npc_lora_request_turns_thinking_off():
    from app.llm import model_body

    b = model_body({"model": GPU_MODEL, "max_tokens": 50}, config.MODEL_PARAMS)
    assert b["chat_template_kwargs"] == {"enable_thinking": False} and "reasoning_effort" not in b
    assert json.dumps(b)  # 그대로 보낼 수 있는 몸통
