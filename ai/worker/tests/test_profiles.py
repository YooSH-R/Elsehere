"""모델 구성 (llm_profiles.json): LLM_PROFILE 하나로 공급자 주소·키·역할별 모델이 바뀌는지 (모델 호출 없음)."""

import json
from pathlib import Path

import httpx
import pytest
from fastapi.testclient import TestClient

from app import config
from app.config import Settings
from app.main import create_app
from conftest import TOKEN, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista at Maple Cafe.", "facts": "Menu: latte $5.", "scene": "Inside Maple Cafe."}
GMS_URL = config.PROFILES["providers"]["gms"]["base_url"]
GROQ_URL = config.PROFILES["providers"]["groq"]["base_url"]


@pytest.fixture
def env(monkeypatch):
    """저장소 .env를 읽지 않는 깨끗한 환경 변수."""
    e = {"AI_WORKER_TOKEN": TOKEN}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", e)
    return e


def test_default_profile_is_gms_luna(env):
    # 2026-10-21 게임 기본을 gms-luna로 (판정 F1 0.803, SOL의 약 1/20 크레딧). 음성 인식은 Groq 그대로
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q"})
    s = Settings.from_env()
    assert (s.profile, s.base_url, s.api_key) == ("gms-luna", GMS_URL, "g")
    # 2026-11-03 NPC 대답만 SOL로 (Luna는 대화가 엉뚱해서). 판정·번역 등은 Luna 그대로, 서로를 대체 모델로
    assert s.models["npc"] == "gpt-6.1-sol" and s.models["guard"] == "gpt-6-luna"
    assert s.fallback == {"gpt-6-luna": "gpt-6.1-sol", "gpt-6.1-sol": "gpt-6-luna"}
    assert s.role_provider == {"stt": "groq"} and s.providers == {"groq": {"base_url": GROQ_URL, "api_key": "q"}}


def test_dabin_role_example_overrides_only_owned_roles(env):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q"})
    before = Settings.from_env()
    example = Path(__file__).resolve().parents[1] / "dabin-roles.env.example"
    overrides = {}
    for line in example.read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            name, value = line.split("=", 1)
            overrides[name] = value
    assert set(overrides) == {"MODEL_MEMORY", "MODEL_FEEDBACK"}
    env.update(overrides)
    after = Settings.from_env()
    assert all(after.models[role] == "gpt-5.4" for role in ("memory", "feedback"))
    for role in before.models:
        if role not in ("memory", "feedback"):
            assert after.models[role] == before.models[role]
    assert after.role_provider == before.role_provider and after.fallback == before.fallback


def test_groq_profile(env):
    env.update({"LLM_PROFILE": "groq", "GROQ_API_KEY": "q"})
    s = Settings.from_env()
    assert (s.profile, s.base_url, s.api_key) == ("groq", GROQ_URL, "q")
    assert s.models["npc"] == "qwen/qwen3.8-27b" and s.models["feedback"] == "openai/gpt-oss-120b"
    assert s.fallback["qwen/qwen3.8-27b"] == "openai/gpt-oss-20b" and s.role_provider == {}


def test_gms_profile_by_one_variable(env):
    env.update({"LLM_PROFILE": "gms-luna", "GMS_KEY": "g", "GROQ_API_KEY": "q", "MODEL_FEEDBACK": "gpt-6.1-sol"})
    s = Settings.from_env()
    assert (s.base_url, s.api_key) == (GMS_URL, "g")
    assert s.models["guard"] == "gpt-6-luna" and s.models["feedback"] == "gpt-6.1-sol"  # 역할 하나만 덮어쓰기
    assert s.role_provider == {"stt": "groq"} and s.providers == {"groq": {"base_url": GROQ_URL, "api_key": "q"}}


def test_stt_fallback_from_profile(env):
    # GMS 구성: 음성 인식이 Groq에서 실패하면 GMS whisper-1로 (GMS는 기본 공급자라 공급자를 따로 더하지 않음)
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q"})
    s = Settings.from_env()
    assert s.stt_fallback == {"provider": "gms", "model": "whisper-1"} and set(s.providers) == {"groq"}
    env["LLM_PROFILE"] = "gms-sol"
    assert Settings.from_env().stt_fallback == {"provider": "gms", "model": "whisper-1"}
    env["LLM_PROFILE"] = "groq"
    assert Settings.from_env().stt_fallback == {}


def test_stt_fallback_by_env(env):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q", "STT_FALLBACK": ""})  # 끄기
    assert Settings.from_env().stt_fallback == {}
    env.update({"LLM_PROFILE": "groq", "STT_FALLBACK": "gms:whisper-1", "STT_FIRST_TIMEOUT_S": "5"})
    s = Settings.from_env()
    assert s.stt_fallback == {"provider": "gms", "model": "whisper-1"} and s.stt_first_timeout_s == 5.0
    assert s.providers["gms"] == {"base_url": GMS_URL, "api_key": "g"}  # 기본 공급자가 아니면 공급자를 더함


@pytest.mark.parametrize("value", ["whisper-1", "nope:whisper-1", "gms:"])
def test_stt_fallback_bad_value(env, value):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q", "STT_FALLBACK": value})
    with pytest.raises(RuntimeError) as e:
        Settings.from_env()
    assert "STT_FALLBACK" in str(e.value)


def test_stt_fallback_key_required(env):
    env.update({"LLM_PROFILE": "groq", "GROQ_API_KEY": "q", "STT_FALLBACK": "gms:whisper-1"})
    with pytest.raises(RuntimeError) as e:
        Settings.from_env()
    assert "GMS_KEY" in str(e.value)


GPU_CHAIN = "gpu:turbo-lr1e4,gms:whisper-1,groq:whisper-large-v3-turbo"


def test_stt_chain_by_env(env):
    # GPU 서버의 자체 모델 → GMS → Groq. gpu 주소는 배포마다 달라(터널 입구) 환경 변수에서 읽는다
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q"})
    assert Settings.from_env().stt_chain == []  # 정하지 않으면 지금처럼 구성의 음성 인식 + STT_FALLBACK
    env.update({"STT_CHAIN": GPU_CHAIN, "STT_GPU_BASE_URL": "http://172.19.0.1:18431/v1/", "STT_GPU_TOKEN": "t"})
    s = Settings.from_env()
    assert s.stt_chain == [
        {"provider": "gpu", "model": "turbo-lr1e4"},
        {"provider": "gms", "model": "whisper-1"},
        {"provider": "groq", "model": "whisper-large-v3-turbo"},
    ]
    assert s.providers["gpu"] == {"base_url": "http://172.19.0.1:18431/v1", "api_key": "t"}
    assert s.providers["groq"] == {"base_url": GROQ_URL, "api_key": "q"}


def test_stt_timeouts_and_skip_by_env(env):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q"})
    s = Settings.from_env()
    assert (s.stt_next_timeout_s, s.stt_breaker_failures, s.stt_breaker_skip_s) == (8.0, 3, 60.0)
    env.update({"STT_NEXT_TIMEOUT_S": "6", "STT_BREAKER_FAILURES": "0", "STT_BREAKER_SKIP_S": "120"})  # 0이면 건너뛰기 끔
    s = Settings.from_env()
    assert (s.stt_next_timeout_s, s.stt_breaker_failures, s.stt_breaker_skip_s) == (6.0, 0, 120.0)


def test_kokoro_threads_by_env(env):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q"})
    assert Settings.from_env().kokoro_threads == 0  # 0이면 onnxruntime 기본 (보이는 코어 수)
    env["KOKORO_THREADS"] = "2"  # 컨테이너 CPU 상한에 맞춤
    assert Settings.from_env().kokoro_threads == 2


@pytest.mark.parametrize(("missing", "named"), [("STT_GPU_BASE_URL", "STT_GPU_BASE_URL"), ("STT_GPU_TOKEN", "STT_GPU_TOKEN")])
def test_stt_chain_gpu_needs_address_and_token(env, missing, named):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q", "STT_CHAIN": GPU_CHAIN, "STT_GPU_BASE_URL": "http://gpu/v1", "STT_GPU_TOKEN": "t"})
    del env[missing]
    with pytest.raises(RuntimeError) as e:
        Settings.from_env()
    assert named in str(e.value)


@pytest.mark.parametrize("value", ["gpu", "nope:x,gms:whisper-1", "gms:whisper-1,", " , "])
def test_stt_chain_bad_value(env, value):
    env.update({"GMS_KEY": "g", "GROQ_API_KEY": "q", "STT_CHAIN": value})
    with pytest.raises(RuntimeError) as e:
        Settings.from_env()
    assert "STT_CHAIN" in str(e.value)


def test_missing_keys_named(env):
    env["LLM_PROFILE"] = "gms-sol"
    with pytest.raises(RuntimeError) as e:
        Settings.from_env()
    assert "GMS_KEY" in str(e.value) and "GROQ_API_KEY" in str(e.value)


def test_requests_go_to_role_provider():
    """GMS 구성: 대화 역할은 GMS로(GPT-6.1 SOL 요청 칸), 음성 인식은 Groq로 간다."""
    seen = []

    async def handler(request: httpx.Request) -> httpx.Response:
        seen.append((request.url.host, request.headers["Authorization"]))
        if request.url.path.endswith("/audio/transcriptions"):
            return httpx.Response(200, json={"text": "Muffin.", "duration": 0.6})
        body = json.loads(request.content)
        seen.append(body)
        return ok(json.dumps({"say_this": [{"en": "a", "ko": "가"}] * 2, "keywords": [{"en": "k", "ko": "케"}]}))

    models, role_provider = config.profile_models("gms-sol")
    s = Settings(
        token=TOKEN,
        base_url="http://gms.test/v1",
        api_key="g",
        profile="gms-sol",
        models=models,
        fallback={},
        role_provider=role_provider,
        providers={"groq": {"base_url": "http://groq.test/v1", "api_key": "q"}},
        call_timeout_s=1.0,
        retry_pause_s=0.0,
    )
    with TestClient(create_app(s, httpx.MockTransport(handler), False), headers={"X-Internal-Token": TOKEN}) as c:
        r = c.post("/v1/hint", json={"npc": NPC, "goal": "order a drink", "history": [{"who": "npc", "text": "Hi!"}]}).json()
        assert r["usage"][0]["model"] == "gpt-6.1-sol"
        c.post("/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")})
    (host, auth), body, stt = seen
    assert (host, auth) == ("gms.test", "Bearer g")
    assert "max_completion_tokens" in body and "temperature" not in body and "max_tokens" not in body
    assert stt == ("groq.test", "Bearer q")
