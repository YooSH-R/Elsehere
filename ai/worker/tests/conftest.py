"""가짜 모델 공급자: 진짜 Groq를 부르지 않고 시스템 프롬프트로 단계를 알아본 뒤 정해 둔 답을 돌려준다."""

import asyncio
import io
import json
import sys
import wave
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
    "# 대화 피드백": "feedback",  # ai/feedback 모듈의 지시문
    "You keep the memory": "memory",
    "You translate one line said by a character": "meaning",
    "You suggest words": "suggest",
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


def tiny_wav(n_frames: int, sr: int = 24000) -> bytes:
    """무음 WAV (16비트 모노, Kokoro와 같은 형식)."""
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"\x00\x00" * n_frames)
    return buf.getvalue()


class FakeTts:
    """wav=True면 진짜 WAV(문장 길이 × 100프레임)를, 아니면 b"RIFF" + 문장을 돌려준다. fail_on: 이 문장은 합성 실패."""

    def __init__(self, delay: float = 0.0, wav: bool = False, fail_on: str | None = None):
        self.delay, self.wav, self.fail_on, self.texts, self.speeds = delay, wav, fail_on, [], []

    async def asynth(self, text, voice, speed=1.0):
        self.texts.append(text)
        self.speeds.append(speed)
        await asyncio.sleep(self.delay)
        if text == self.fail_on:
            raise RuntimeError("synth failed")
        audio = tiny_wav(len(text) * 100) if self.wav else b"RIFF" + text.encode()
        return audio, len(text) / 15, False


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
        "stt_fallback": {},
        **kw,
    }
    s = Settings(token=TOKEN, base_url="http://fake/v1", api_key="x", **{"call_timeout_s": 1.0, "retry_pause_s": 0.0, **kw})
    return TestClient(create_app(s, httpx.MockTransport(provider.handler), tts, guard_local), headers={"X-Internal-Token": TOKEN})


@pytest.fixture
def client(provider):
    with make_client(provider) as c:
        yield c


NPC_EXAMPLES = Path(__file__).resolve().parents[3] / "docs/api/npc-ai-contracts/npc_configuration_examples_v2.json"


def load_npc_examples() -> list[dict]:
    """공통 인격 스키마의 계약 예시 NPC 3명 (카페 점원·호텔리어·행인). 부를 때마다 새로 읽어서 고쳐도 다른 테스트에 번지지 않는다."""
    return json.loads(NPC_EXAMPLES.read_text(encoding="utf-8"))


def cafe_with(path: str, value) -> dict:
    """계약 예시의 카페 점원 설정에서 path(점으로 구분한 키) 한 곳만 value로 바꾼 것."""
    cafe = load_npc_examples()[0]
    *parents, key = path.split(".")
    target = cafe
    for name in parents:
        target = target[name]
    target[key] = value
    return cafe
