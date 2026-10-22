"""가짜 모델 공급자: 진짜 Groq를 부르지 않고 시스템 프롬프트로 단계를 알아본 뒤 정해 둔 답을 돌려준다."""

import asyncio
import json
import sys
from pathlib import Path

import httpx
import pytest
from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from app.config import PROFILES, Settings, profile_models
from app.main import create_app

TOKEN = "test-token"
STEPS = {
    "You moderate": "guard",
    "You label": "intent",
    "You help a Korean learner of English in a role-play": "translate",
    "You help a Korean learner of English (CEFR A2-B1) who is stuck": "hint",
    "You give short, kind feedback": "feedback",
    "You keep the memory": "memory",
}


def step_of(body: dict) -> str:
    sys_msg = body["messages"][0]["content"]
    return next((v for k, v in STEPS.items() if sys_msg.startswith(k)), "npc")


def ok(content: str, usage=(100, 5)) -> httpx.Response:
    return httpx.Response(
        200, json={"choices": [{"message": {"content": content}}], "usage": {"prompt_tokens": usage[0], "completion_tokens": usage[1]}}
    )


def stream(pieces: list[str]) -> httpx.Response:
    lines = [f"data: {json.dumps({'choices': [{'delta': {'content': p}}]})}" for p in pieces]
    lines.append(f"data: {json.dumps({'choices': [], 'usage': {'prompt_tokens': 300, 'completion_tokens': 20}})}")
    return httpx.Response(200, content=("\n\n".join(lines + ["data: [DONE]"]) + "\n\n").encode())


class Provider:
    """answers[단계] = 함수(body) → httpx.Response. calls에 (단계, 모델)을 남긴다."""

    def __init__(self):
        self.answers, self.calls = {}, []

    async def handler(self, request: httpx.Request) -> httpx.Response:
        if request.url.path.endswith("/audio/transcriptions"):
            self.calls.append(("stt", "whisper"))
            self.last_stt = request.content
            return self.answers["stt"](None)
        body = json.loads(request.content)
        step = step_of(body)
        self.calls.append((step, body["model"]))
        if step not in self.answers:
            raise AssertionError(f"정하지 않은 단계 호출: {step}")
        res = self.answers[step](body)
        return await res if asyncio.iscoroutine(res) else res  # async 함수면 늦게 답하는 공급자


class FakeTts:
    def __init__(self, delay: float = 0.0):
        self.delay, self.texts = delay, []

    async def asynth(self, text, voice, speed=1.0):
        self.texts.append(text)
        await asyncio.sleep(self.delay)
        return b"RIFF" + text.encode(), len(text) / 15, False


@pytest.fixture
def provider():
    return Provider()


def make_client(provider: Provider, tts=None, guard_local=None, **kw) -> TestClient:
    # 재시도·대체 모델 사슬은 groq 구성(qwen → gpt-oss-20b → 120b)으로 확인한다. 기본 구성(gms-luna)과 상관없이 고정
    kw = {
        "profile": "groq",
        "models": profile_models("groq")[0],
        "role_provider": {},
        "fallback": dict(PROFILES["profiles"]["groq"]["fallback"]),
        **kw,
    }
    s = Settings(token=TOKEN, base_url="http://fake/v1", api_key="x", call_timeout_s=1.0, retry_pause_s=0.0, **kw)
    return TestClient(create_app(s, httpx.MockTransport(provider.handler), tts, guard_local), headers={"X-Internal-Token": TOKEN})


@pytest.fixture
def client(provider):
    with make_client(provider) as c:
        yield c
