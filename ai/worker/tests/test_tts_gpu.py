"""GPU 서버 음성 합성 (ai/gpu_tts). 워커가 다듬고 음소로 바꾼 글을 GPU 서버에 먼저 보내고, 실패·시간 초과면 CPU Kokoro로 만든다.

CPU 합성은 대사 하나에 약 2~3초(dev 2코어)라 NPC 대답 조각이 2.5초 상한에 걸려 음성이 빠졌다. GPU는 약 0.4~0.6초 (S15P31A305-370)."""

import asyncio
import json
import threading

import httpx
import pytest

from app import config
from app.config import Settings
from app.tts import GpuSynth, Kokoro
from conftest import TOKEN, tiny_wav

GPU_URL = "http://172.19.0.1:18433"


class IdentityTokenizer:
    """espeak 음소화 자리. 글을 그대로 돌려준다."""

    def phonemize(self, text, lang="en-us"):
        return text.strip()


class RecordingEngine:
    """CPU Kokoro 엔진 자리. 받은 음소만 적고 실제 합성은 하지 않는다 (테스트 환경에 numpy·모델이 없음)."""

    def __init__(self):
        self.tokenizer = IdentityTokenizer()
        self.texts = []

    def create(self, text, voice, speed, lang, is_phonemes=False):
        self.texts.append(text)
        raise RuntimeError("CPU 합성은 하지 않음")


class GpuServer:
    """GPU 합성 서버 자리. answer(요청) → httpx.Response, 또는 예외를 던진다."""

    def __init__(self, answer):
        self.answer, self.requests = answer, []

    def handler(self, request: httpx.Request) -> httpx.Response:
        self.requests.append(request)
        return self.answer(request)


class FakeClock:
    def __init__(self):
        self.now = 1000.0

    def __call__(self):
        return self.now


def wav_answer(n_frames=24000):
    return lambda request: httpx.Response(200, content=tiny_wav(n_frames), headers={"Content-Type": "audio/wav"})


def kokoro_with_gpu(server: GpuServer, **kw) -> tuple[Kokoro, RecordingEngine]:
    engine = RecordingEngine()
    gpu = GpuSynth(GPU_URL, "secret", transport=httpx.MockTransport(server.handler), **{"timeout_s": 2.0, **kw})
    return Kokoro(engine, "fake.onnx", gpu), engine


def test_gpu_synthesizes_first_and_cpu_is_not_used():
    server = GpuServer(wav_answer(36000))
    kokoro, engine = kokoro_with_gpu(server)

    wav, seconds, cached = kokoro.synth("That's $13, hon.", "af_heart", 0.85)

    assert wav == tiny_wav(36000) and seconds == 1.5 and cached is False
    assert engine.texts == []
    request = server.requests[0]
    assert request.url == f"{GPU_URL}/v1/synth"
    assert request.headers["Authorization"] == "Bearer secret"
    # 읽을 글 다듬기·발음 사전은 워커가 하고 GPU 서버는 음소만 받는다
    assert json.loads(request.content) == {"phonemes": "That's 13 dollars, hˈʌn.", "voice": "af_heart", "speed": 0.85}


def test_gpu_error_falls_back_to_cpu():
    server = GpuServer(lambda request: httpx.Response(503, text="down"))
    kokoro, engine = kokoro_with_gpu(server)

    with pytest.raises(RuntimeError, match="CPU 합성"):
        kokoro.synth("Hello there.", "af_heart", 1.0)

    assert len(server.requests) == 1  # GPU는 다시 시도하지 않는다
    assert engine.texts == ["Hello there."]


def test_gpu_timeout_falls_back_to_cpu():
    def slow(request):
        raise httpx.ReadTimeout("no answer", request=request)

    kokoro, engine = kokoro_with_gpu(GpuServer(slow))

    with pytest.raises(RuntimeError, match="CPU 합성"):
        kokoro.synth("Hello there.", "af_heart", 1.0)

    assert engine.texts == ["Hello there."]


def test_cancelled_gpu_request_does_not_start_cpu_fallback():
    started, release = threading.Event(), threading.Event()

    class WaitingGpu:
        def try_synth(self, *args):
            started.set()
            assert release.wait(2)
            return None

    engine = RecordingEngine()
    kokoro = Kokoro(engine, "fake.onnx", WaitingGpu())

    async def scenario():
        task = asyncio.create_task(kokoro.asynth("Hello there.", "af_heart", 1.0))
        try:
            assert await asyncio.to_thread(started.wait, 1)
            task.cancel()
            with pytest.raises(asyncio.CancelledError):
                await task
        finally:
            release.set()

    asyncio.run(scenario())
    assert engine.texts == []


def test_gpu_waits_long_for_a_busy_server_but_gives_up_connecting_quickly():
    """여러 사람이 동시에 말하면 GPU 서버 앞에 줄이 선다. 2초 만에 CPU로 넘기면 CPU 합성(dev 2코어, 조각 하나 수 초~수십 초)이
    잠금 안에서 하나씩 돌아 턴이 수십 초 늦어졌다 (S15P31A305-486 측정). 응답은 넉넉히 기다리고, 연결(꺼진 서버·끊긴 터널)만 짧게 끊는다."""
    server = GpuServer(wav_answer())
    kokoro, _ = kokoro_with_gpu(server, timeout_s=10.0, connect_timeout_s=1.0)

    kokoro.synth("Hello there.", "af_heart", 1.0)

    timeout = server.requests[0].extensions["timeout"]
    assert timeout["connect"] == 1.0 and timeout["read"] == 10.0


def test_gpu_connection_failure_falls_back_to_cpu():
    def down(request):
        raise httpx.ConnectError("connection refused", request=request)

    kokoro, engine = kokoro_with_gpu(GpuServer(down))

    with pytest.raises(RuntimeError, match="CPU 합성"):
        kokoro.synth("Hello there.", "af_heart", 1.0)

    assert engine.texts == ["Hello there."]


def test_gpu_answer_that_is_not_wav_falls_back_to_cpu():
    kokoro, engine = kokoro_with_gpu(GpuServer(lambda request: httpx.Response(200, content=b"<html>proxy error</html>")))

    with pytest.raises(RuntimeError, match="CPU 합성"):
        kokoro.synth("Hello there.", "af_heart", 1.0)

    assert engine.texts == ["Hello there."]


def test_gpu_is_skipped_after_repeated_failures_and_tried_again_later():
    clock = FakeClock()
    server = GpuServer(lambda request: httpx.Response(503, text="down"))
    kokoro, engine = kokoro_with_gpu(server, breaker_failures=2, breaker_skip_s=60.0, clock=clock)

    for i in range(3):
        with pytest.raises(RuntimeError):
            kokoro.synth(f"Line {i}.", "af_heart", 1.0)
    assert len(server.requests) == 2  # 두 번 연속 실패 뒤 세 번째는 GPU를 건너뛰고 바로 CPU
    assert len(engine.texts) == 3

    clock.now += 61
    server.answer = wav_answer()
    kokoro.synth("Line 3.", "af_heart", 1.0)
    assert len(server.requests) == 3


def test_gpu_success_resets_failure_count():
    clock = FakeClock()
    answers = iter([httpx.Response(503), httpx.Response(200, content=tiny_wav(100)), httpx.Response(503), httpx.Response(503)])
    server = GpuServer(lambda request: next(answers))
    kokoro, _ = kokoro_with_gpu(server, breaker_failures=2, breaker_skip_s=60.0, clock=clock)

    for i in range(4):
        try:
            kokoro.synth(f"Line {i}.", "af_heart", 1.0)
        except RuntimeError:
            pass

    assert len(server.requests) == 4  # 실패 → 성공(초기화) → 실패 → 실패: 연속 두 번째 실패까지는 계속 GPU를 부른다


def test_same_line_is_not_sent_to_gpu_again():
    server = GpuServer(wav_answer())
    kokoro, _ = kokoro_with_gpu(server)

    kokoro.synth("Welcome back!", "af_heart", 1.0)
    _, _, cached = kokoro.synth("Welcome back!", "af_heart", 1.0)

    assert cached is True and len(server.requests) == 1


def test_nothing_to_read_does_not_call_gpu():
    server = GpuServer(wav_answer())
    kokoro, _ = kokoro_with_gpu(server)

    kokoro.synth("감사합니다!", "af_heart", 1.0)

    assert server.requests == []


@pytest.fixture
def env(monkeypatch):
    e = {"AI_WORKER_TOKEN": TOKEN, "GMS_KEY": "g", "GROQ_API_KEY": "q"}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", e)
    return e


def test_gpu_synth_is_off_by_default(env):
    s = Settings.from_env()
    assert s.tts_gpu_base_url == "" and GpuSynth.from_settings(s) is None


def test_gpu_waits_by_default_long_enough_for_a_queue_of_concurrent_lines(env):
    # 동시 15요청(5명 × 조각 3)을 GPU가 동시에 합성하면 약 2~2.6초, 60요청(20명분)이면 약 4.4~6.1초 (S15P31A305-486 실측)
    s = Settings.from_env()
    assert (s.tts_gpu_connect_timeout_s, s.tts_gpu_timeout_s) == (1.0, 10.0)


def test_gpu_synth_from_env(env):
    env["TTS_GPU_BASE_URL"] = GPU_URL
    with pytest.raises(RuntimeError, match="TTS_GPU_TOKEN"):
        Settings.from_env()
    env.update(
        {
            "TTS_GPU_TOKEN": "t",
            "TTS_GPU_TIMEOUT_S": "1.5",
            "TTS_GPU_CONNECT_TIMEOUT_S": "0.5",
            "TTS_BREAKER_FAILURES": "0",
            "TTS_BREAKER_SKIP_S": "120",
        }
    )
    s = Settings.from_env()
    assert (s.tts_gpu_base_url, s.tts_gpu_token, s.tts_gpu_timeout_s, s.tts_gpu_connect_timeout_s) == (GPU_URL, "t", 1.5, 0.5)
    assert (s.tts_breaker_failures, s.tts_breaker_skip_s) == (0, 120.0)
    assert GpuSynth.from_settings(s) is not None
