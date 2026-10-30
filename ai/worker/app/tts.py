"""음성 합성 (Kokoro-82M 8비트 ONNX, CPU)과 스트림 음성 조각 보관.

모델 파일이 없거나 kokoro-onnx가 설치되지 않았으면 합성 없이 돌아간다: NPC 대답 스트림은 조각마다 tts: false를 보내고,
/v1/tts는 tts_unavailable(503)을 돌려준다. 화면은 자막만 보여 주거나 브라우저 음성으로 읽는다.
측정 (i7-13700H): 한 문장 0.44~1.16초. 같은 PC에서 3D 화면이 CPU를 많이 쓰면 몇 배 느려진다 (2026-10-14).
"""

import asyncio
import io
import logging
import secrets
import threading
import time
import wave
from collections import OrderedDict
from pathlib import Path

from .speech_alignment import align_timing
from .speech_timing import attach_timing, join_timing, read_timing

log = logging.getLogger(__name__)


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
    return join_timing(buf.getvalue(), wavs), n / params.framerate


class Kokoro:
    """합성은 한 번에 하나 (CPU를 나눠 쓰면 둘 다 느려짐). 같은 문장(인사, 다시 듣기, 단어 발음)은 다시 만들지 않는다."""

    CACHE_MAX = 300

    def __init__(self, model_path: str, voices_path: str):
        from kokoro_onnx import Kokoro as Engine  # 선택 의존성: 없으면 load()가 None을 돌려줌

        self.engine = Engine(model_path, voices_path)
        self.name = Path(model_path).name
        self.phoneme_timing_ready = False
        if not self.engine.has_timings:
            log.warning("Kokoro model has no duration output; NPC phoneme animation is unavailable")
        self.lock = threading.Lock()
        self.cache: OrderedDict = OrderedDict()

    @classmethod
    def load(cls, model_path: str, voices_path: str) -> "Kokoro | None":
        if not model_path or not Path(model_path).exists() or not Path(voices_path or "").exists():
            return None
        try:
            k = cls(model_path, voices_path)
        except ImportError:
            return None
        warmup, _, _ = k.synth("Ready.", "af_heart", 1.0)
        k.phoneme_timing_ready = read_timing(warmup) is not None
        return k

    def synth(self, text: str, voice: str, speed: float) -> tuple[bytes, float, bool]:
        """반환: (wav, 소리 길이 초, 저장본인지)."""
        key = (text, voice, speed)
        with self.lock:
            if key in self.cache:
                self.cache.move_to_end(key)
                wav, dur = self.cache[key]
                return wav, dur, True
            if self.engine.has_timings:
                audio, sr, spoken = self.engine.create_timed(text, voice=voice, speed=speed, lang="en-us")
            else:
                audio, sr = self.engine.create(text, voice=voice, speed=speed, lang="en-us")
                spoken = []
            wav, dur = to_wav(audio, sr), len(audio) / sr
            try:
                timing = read_timing(attach_timing(wav, spoken))
                if timing:
                    wav = align_timing(wav, timing["phonemes"])
            except (ValueError, RuntimeError):
                # Keep the real voice available, but never manufacture phoneme times.
                log.warning("Kokoro phoneme timing was rejected; returning voice without lip sync", exc_info=True)
            self.cache[key] = (wav, dur)
            if len(self.cache) > self.CACHE_MAX:
                self.cache.popitem(last=False)
        return wav, dur, False

    async def asynth(self, text: str, voice: str, speed: float = 1.0) -> tuple[bytes, float, bool]:
        return await asyncio.to_thread(self.synth, text, voice, speed)


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
        item = self.items.get(audio_id)
        if not item or item[0] < time.time():
            return None
        return item[1]
