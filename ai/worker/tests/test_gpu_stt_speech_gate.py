"""GPU STT 서버가 받아 적기 전에 말소리 없는 녹음을 거르는 판정 (S15P31A305-704).

faster-whisper(Silero VAD 포함)는 GPU 서버 환경에만 설치돼 워커 CI에는 없다. 없으면 건너뛴다.
GPU 서버나 faster-whisper가 설치된 환경에서 실행한다: python -m pytest ai/worker/tests/test_gpu_stt_speech_gate.py
"""

import runpy
import wave
from pathlib import Path

import pytest

np = pytest.importorskip("numpy")
pytest.importorskip("faster_whisper", reason="GPU 서버 환경(faster-whisper)에서만 실행")

heard_speech = runpy.run_path(str(Path(__file__).resolve().parents[2] / "gpu_stt/speech_gate.py"))["heard_speech"]
FIXTURES = Path(__file__).resolve().parent / "fixtures"
RATE = 16000


def recording(name: str) -> np.ndarray:
    with wave.open(str(FIXTURES / name), "rb") as w:
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float32) / 32768.0


def white_noise(level: float, seconds: float = 3.0) -> np.ndarray:
    return np.random.default_rng(0).normal(0, level, int(RATE * seconds)).astype(np.float32)


def test_silence_is_not_speech():
    assert not heard_speech(np.zeros(RATE * 2, dtype=np.float32))


@pytest.mark.parametrize("level", [0.002, 0.03, 0.06])
def test_steady_noise_is_not_speech(level):
    # 앱이 말한 것으로 보는 음량(0.02)을 넘는 잡음도 말소리가 아니다
    assert not heard_speech(white_noise(level))


def test_room_hum_is_not_speech():
    t = np.arange(RATE * 3) / RATE
    assert not heard_speech((0.03 * np.sin(2 * np.pi * 60 * t)).astype(np.float32))


@pytest.mark.parametrize("name", ["speech_yes.wav", "speech_latte.wav"])
def test_one_short_word_is_speech(name):
    assert heard_speech(recording(name))


def test_short_word_between_silence_and_noise_is_speech():
    word = recording("speech_yes.wav")
    padded = np.concatenate([np.zeros(RATE // 2, dtype=np.float32), word, np.zeros(RATE, dtype=np.float32)])
    assert heard_speech(padded + white_noise(0.01, len(padded) / RATE))
