"""모델 구성 (llm_profiles.json): LLM_PROFILE 하나로 공급자 주소·키·역할별 모델이 바뀌는지 (모델 호출 없음)."""
import json

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
    assert s.models["npc"] == "gpt-6-luna" and s.fallback == {"gpt-6-luna": "gpt-6.1-sol"}
    assert s.role_provider == {"stt": "groq"} and s.providers == {"groq": {"base_url": GROQ_URL, "api_key": "q"}}


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
    assert s.models["npc"] == "gpt-6-luna" and s.models["feedback"] == "gpt-6.1-sol"   # 역할 하나만 덮어쓰기
    assert s.role_provider == {"stt": "groq"} and s.providers == {"groq": {"base_url": GROQ_URL, "api_key": "q"}}


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
    s = Settings(token=TOKEN, base_url="http://gms.test/v1", api_key="g", profile="gms-sol", models=models, fallback={},
                 role_provider=role_provider, providers={"groq": {"base_url": "http://groq.test/v1", "api_key": "q"}},
                 call_timeout_s=1.0, retry_pause_s=0.0)
    with TestClient(create_app(s, httpx.MockTransport(handler), False), headers={"X-Internal-Token": TOKEN}) as c:
        r = c.post("/v1/hint", json={"npc": NPC, "goal": "order a drink", "history": [{"who": "npc", "text": "Hi!"}]}).json()
        assert r["usage"][0]["model"] == "gpt-6.1-sol"
        c.post("/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")})
    (host, auth), body, stt = seen
    assert (host, auth) == ("gms.test", "Bearer g")
    assert "max_completion_tokens" in body and "temperature" not in body and "max_tokens" not in body
    assert stt == ("groq.test", "Bearer q")
