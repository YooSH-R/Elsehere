"""음성 합성 (Kokoro-82M 8비트 ONNX, CPU. GPU 서버가 있으면 그쪽 먼저)과 스트림 음성 조각 보관.

모델 파일이 없거나 kokoro-onnx가 설치되지 않았으면 합성 없이 돌아간다: NPC 대답 스트림은 조각마다 tts: false를 보내고,
/v1/tts는 tts_unavailable(503)을 돌려준다. 화면은 자막만 보여 주거나 브라우저 음성으로 읽는다.
측정 (i7-13700H): 한 문장 0.44~1.16초. 같은 PC에서 3D 화면이 CPU를 많이 쓰면 몇 배 느려진다 (2026-10-14).
CPU 2스레드(dev 설정)는 대사 하나 약 2~3초, 가끔 20초. GPU 서버(H200, fp32)는 약 0.4~0.6초 (2026-10-30, S15P31A305-370).
"""

import asyncio
import io
import json
import logging
import secrets
import threading
import time
import wave
from collections import OrderedDict
from pathlib import Path
from types import SimpleNamespace

import httpx

from .pronunciation import to_phonemes
from .speech_alignment import align_timing
from .speech_emotion import join_emotions
from .speech_text import for_speech
from .speech_timing import attach_timing, join_timing, phoneme_sequence, read_timing

log = logging.getLogger("ai_worker")


def to_wav(audio, sr: int) -> bytes:
    import numpy as np

    pcm = (np.clip(audio, -1, 1) * 32767).astype("<i2").tobytes()
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm)
    return buf.getvalue()


def join_wavs(wavs: list[bytes]) -> tuple[bytes, float]:
    """같은 형식의 WAV 조각을 순서대로 이어 붙인다 (한 턴의 다시 듣기용 음성). 반환: (wav, 길이 초)."""
    frames, params = [], None
    for data in wavs:
        with wave.open(io.BytesIO(data)) as w:
            params = params or w.getparams()
            frames.append(w.readframes(w.getnframes()))
    buf = io.BytesIO()
    with wave.open(buf, "wb") as out:
        out.setnchannels(params.nchannels)
        out.setsampwidth(params.sampwidth)
        out.setframerate(params.framerate)
        out.writeframes(b"".join(frames))
    n = sum(len(f) for f in frames) // (params.sampwidth * params.nchannels)
    return join_emotions(join_timing(buf.getvalue(), wavs), wavs), n / params.framerate


def silent_wav(seconds: float, sr: int = 24000) -> bytes:
    """Kokoro 음성과 같은 형식의 무음 (턴 전체 음성으로 이어 붙일 수 있게)."""
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"\x00\x00" * round(sr * seconds))
    # This PCM is deliberately silent; retain a real rest interval when joining replies.
    rest = SimpleNamespace(phoneme=" ", start=0, end=round(sr * seconds) / sr)
    return attach_timing(buf.getvalue(), [rest])


class GpuSynth:
    """SSAFY GPU 서버의 Kokoro 합성 서버(ai/gpu_tts). 워커가 다듬고 음소로 바꾼 글을 보내 WAV를 받는다.
    한 번만 부르고(재시도 없음), 실패·시간 초과면 None을 돌려줘 CPU 합성으로 넘긴다.
    연속 실패가 breaker_failures에 닿으면 breaker_skip_s 동안 부르지 않는다 (GPU 서버가 꺼졌을 때 매번 상한만큼 기다리지 않게)."""

    def __init__(
        self,
        base_url: str,
        token: str,
        timeout_s: float,
        breaker_failures: int = 3,
        breaker_skip_s: float = 60.0,
        transport: httpx.BaseTransport | None = None,
        clock=time.monotonic,
        connect_timeout_s: float = 1.0,
    ):
        self.url = base_url.rstrip("/") + "/v1/synth"
        self.client = httpx.Client(
            transport=transport,
            timeout=httpx.Timeout(timeout_s, connect=connect_timeout_s),
            headers={"Authorization": f"Bearer {token}"},
        )
        self.breaker_failures, self.breaker_skip_s, self.clock = breaker_failures, breaker_skip_s, clock
        self.failures = 0  # 연속 실패 수
        self.skip_until = 0.0  # 이 시각(clock)까지 GPU를 건너뜀

    @classmethod
    def from_settings(cls, s) -> "GpuSynth | None":
        if not s.tts_gpu_base_url:
            return None
        return cls(
            s.tts_gpu_base_url,
            s.tts_gpu_token,
            s.tts_gpu_timeout_s,
            s.tts_breaker_failures,
            s.tts_breaker_skip_s,
            connect_timeout_s=s.tts_gpu_connect_timeout_s,
        )

    def try_synth(self, phonemes: str, voice: str, speed: float) -> tuple[bytes, float] | None:
        """반환: (wav, 소리 길이 초). 건너뛰는 중이거나 실패하면 None."""
        if self.skip_until > self.clock():
            return None
        try:
            r = self.client.post(self.url, json={"phonemes": phonemes, "voice": voice, "speed": speed})
            r.raise_for_status()
            wav = r.content
            with wave.open(io.BytesIO(wav)) as w:
                seconds = w.getnframes() / w.getframerate()
        except (httpx.HTTPError, wave.Error, EOFError) as e:
            self._note_failure(e)
            return None
        self.failures = 0
        return wav, seconds

    def _note_failure(self, error: Exception):
        log.warning(json.dumps({"event": "tts_gpu_failed", "error": type(error).__name__}))
        if self.breaker_failures <= 0:
            return
        self.failures += 1
        if self.failures >= self.breaker_failures:
            self.skip_until = self.clock() + self.breaker_skip_s
            self.failures = 0
            log.warning(json.dumps({"event": "tts_gpu_skip", "skip_s": self.breaker_skip_s}))


class Kokoro:
    """합성은 한 번에 하나 (CPU를 나눠 쓰면 둘 다 느려짐). 같은 문장(인사, 다시 듣기, 단어 발음)은 다시 만들지 않는다.
    읽는 글은 for_speech로 다듬은 것이다 (가격·시각을 말로, 별표·한글은 읽지 않음). 자막은 호출한 쪽의 원문 그대로.
    다듬은 글은 발음 사전(app.pronunciation)을 거쳐 음소로 넘긴다.
    gpu가 있으면 그 음소를 GPU 서버에 먼저 보내고, 실패하면 CPU로 만든다 (GPU는 fp32 모델이라 CPU int8과 소리가 아주 조금 다르다)."""

    CACHE_MAX = 300
    NOTHING_TO_READ_S = 0.2

    def __init__(self, engine, name: str, gpu: GpuSynth | None = None):
        self.engine = engine
        self.name = name
        self.phoneme_timing_ready = False
        if not getattr(self.engine, "has_timings", False):
            log.warning("CPU Kokoro model has no duration output; fallback phoneme animation is unavailable")
        self.gpu = gpu
        self.lock = threading.Lock()
        self.cache: OrderedDict = OrderedDict()

    @classmethod
    def load(cls, model_path: str, voices_path: str, threads: int = 0, gpu: GpuSynth | None = None) -> "Kokoro | None":
        if not model_path or not Path(model_path).exists() or not Path(voices_path or "").exists():
            return None
        try:
            import onnxruntime as ort  # 선택 의존성(kokoro-onnx가 함께 설치): 없으면 합성 없이 돈다
            from kokoro_onnx import Kokoro as Engine
        except ImportError:
            return None
        options = ort.SessionOptions()
        options.intra_op_num_threads = threads  # 0이면 onnxruntime 기본 (Settings.kokoro_threads)
        session = ort.InferenceSession(model_path, options, providers=["CPUExecutionProvider"])
        k = cls(Engine.from_session(session, voices_path), Path(model_path).name)
        warmup, _, _ = k.synth("Ready.", "af_heart", 1.0)
        k.phoneme_timing_ready = read_timing(warmup) is not None
        k.gpu = gpu
        return k

    def synth(self, text: str, voice: str, speed: float, cancelled: threading.Event | None = None) -> tuple[bytes, float, bool]:
        """반환: (wav, 소리 길이 초, 저장본인지). 읽을 것이 없으면(한글만 있는 조각 등) 짧은 무음."""
        spoken = for_speech(text)
        if not spoken:
            return silent_wav(self.NOTHING_TO_READ_S), self.NOTHING_TO_READ_S, False
        key = (spoken, voice, speed)
        with self.lock:
            if cancelled is not None and cancelled.is_set():
                raise asyncio.CancelledError
            if key in self.cache:
                self.cache.move_to_end(key)
                wav, dur = self.cache[key]
                return wav, dur, True
            spoken_phonemes = to_phonemes(spoken, lambda part: self.engine.tokenizer.phonemize(part, "en-us"))
        # Keep the GPU wait outside the CPU synthesis lock.
        made = self.gpu.try_synth(spoken_phonemes, voice, speed) if self.gpu else None
        with self.lock:
            # 실행 중인 추론 자체는 멈출 수 없지만, 연결 종료 뒤 대체 합성은 시작하지 않는다.
            if cancelled is not None and cancelled.is_set():
                raise asyncio.CancelledError
            if made is None:
                if getattr(self.engine, "has_timings", False):
                    audio, sr, phonemes = self.engine.create_timed(
                        spoken_phonemes, voice=voice, speed=speed, lang="en-us", is_phonemes=True
                    )
                else:
                    audio, sr = self.engine.create(spoken_phonemes, voice=voice, speed=speed, lang="en-us", is_phonemes=True)
                    phonemes = []
                wav, dur = to_wav(audio, sr), len(audio) / sr
                try:
                    model_timed_wav = attach_timing(wav, phonemes)
                    timing = read_timing(model_timed_wav)
                    if timing:
                        try:
                            wav = align_timing(wav, timing["phonemes"])
                        except RuntimeError:
                            # 정렬기 실행 실패는 같은 합성의 유효한 모델 시간을 버릴 이유가 아니다.
                            # 발음 불일치(ValueError)는 기존처럼 시간표를 사용하지 않는다.
                            wav = model_timed_wav
                            log.warning("Acoustic alignment unavailable; preserving validated model phoneme timing", exc_info=True)
                except (ValueError, RuntimeError):
                    log.warning("Kokoro phoneme timing was rejected; returning voice without lip sync", exc_info=True)
            else:
                wav, dur = made
                try:
                    wav = align_timing(wav, phoneme_sequence(spoken_phonemes))
                except (ValueError, RuntimeError):
                    log.warning("GPU phoneme alignment was rejected; returning voice without lip sync", exc_info=True)
            self.cache[key] = (wav, dur)
            if len(self.cache) > self.CACHE_MAX:
                self.cache.popitem(last=False)
        return wav, dur, False

    async def asynth(self, text: str, voice: str, speed: float = 1.0) -> tuple[bytes, float, bool]:
        cancelled = threading.Event()
        try:
            return await asyncio.to_thread(self.synth, text, voice, speed, cancelled)
        except asyncio.CancelledError:
            cancelled.set()
            raise


class AudioStore:
    """NPC 대답 스트림의 음성 조각. audio_id로 GET /v1/audio/{id}에서 가져간다 (보관 시간이 지나면 버림)."""

    def __init__(self, ttl_s: float = 600, max_items: int = 2000, max_bytes: int = 200_000_000):
        self.ttl, self.max, self.max_bytes, self.total = ttl_s, max_items, max_bytes, 0
        self.items: OrderedDict[str, tuple[float, bytes]] = OrderedDict()

    def put(self, wav: bytes) -> str:
        """보관 시간이 지난 것, 개수·전체 크기 상한을 넘는 것은 오래된 것부터 버린다 (방금 넣는 것은 커도 보관)."""
        now = time.time()
        while self.items and (
            len(self.items) >= self.max or self.total + len(wav) > self.max_bytes or next(iter(self.items.values()))[0] < now
        ):
            self.total -= len(self.items.popitem(last=False)[1][1])
        audio_id = secrets.token_hex(8)
        self.items[audio_id] = (now + self.ttl, wav)
        self.total += len(wav)
        return audio_id

    def get(self, audio_id: str) -> bytes | None:
        self.expire()
        item = self.items.get(audio_id)
        return item[1] if item else None

    def expire(self) -> None:
        now = time.time()
        while self.items and next(iter(self.items.values()))[0] <= now:
            self.total -= len(self.items.popitem(last=False)[1][1])
