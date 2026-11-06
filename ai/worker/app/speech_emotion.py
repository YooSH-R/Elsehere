"""표정 시간표를 음성 PCM에 묶는다. 기존 발음 시간표와 PCM은 그대로 보존한다."""

import hashlib
import io
import json
import logging
import math
import struct
import wave
from dataclasses import dataclass

log = logging.getLogger(__name__)

EMOTIONS = (
    "calm",
    "warm_smile",
    "curious",
    "confused",
    "frown",
    "surprised",
    "playful",
    "laugh",
    "concerned",
    "sad",
    "big_laugh",
    "indifferent",
)


@dataclass(frozen=True)
class Mood:
    emotion: str
    intensity: float

    def __post_init__(self):
        if self.emotion not in EMOTIONS or not math.isfinite(self.intensity) or not 0 <= self.intensity <= 1:
            raise ValueError("Invalid speech emotion")


def identity(wav):
    with wave.open(io.BytesIO(wav)) as source:
        pcm = source.readframes(source.getnframes())
        return hashlib.sha256(pcm).hexdigest(), source.getnframes() / source.getframerate()


def chunks(wav):
    offset = 12
    while offset + 8 <= len(wav):
        tag, size = struct.unpack_from("<4sI", wav, offset)
        end = offset + 8 + size
        if end + size % 2 > len(wav):
            raise ValueError("Truncated WAV chunk")
        yield tag, wav[offset + 8 : end], wav[offset : end + size % 2]
        offset = end + size % 2


def write_emotions(wav, cues):
    digest, duration = identity(wav)
    payload = json.dumps({"version": 1, "duration": duration, "pcmSha256": digest, "cues": cues}, separators=(",", ":")).encode()
    body = b"".join(raw for tag, _, raw in chunks(wav) if tag != b"e305")
    body += b"e305" + struct.pack("<I", len(payload)) + payload + b"\0" * (len(payload) % 2)
    return b"RIFF" + struct.pack("<I", len(body) + 4) + b"WAVE" + body


def attach_emotion(wav: bytes, mood: Mood | None) -> bytes:
    if mood is None:
        return wav
    _, duration = identity(wav)
    return write_emotions(wav, [{"start": 0, "end": duration, "emotion": mood.emotion, "intensity": mood.intensity}])


def read_emotions(wav: bytes) -> dict | None:
    payloads = [payload for tag, payload, _ in chunks(wav) if tag == b"e305"]
    if not payloads:
        return None
    if len(payloads) != 1 or len(payloads[0]) > 512 * 1024:
        raise ValueError("Invalid emotion chunk")
    data = json.loads(payloads[0])
    digest, duration = identity(wav)
    if (
        data["version"] != 1
        or data["pcmSha256"] != digest
        or not math.isfinite(data["duration"])
        or abs(data["duration"] - duration) > 1e-6
    ):
        raise ValueError("Emotions do not belong to this voice")
    previous = 0
    for cue in data["cues"]:
        Mood(cue["emotion"], cue["intensity"])
        start, end = cue["start"], cue["end"]
        if not all(math.isfinite(v) for v in (start, end)) or start < previous or end <= start or end > duration + 1e-6:
            raise ValueError("Invalid emotion interval")
        previous = end
    return data


def join_emotions(wav: bytes, parts: list[bytes]) -> bytes:
    cues, offset = [], 0.0
    for part in parts:
        try:
            data = read_emotions(part)
        except (ValueError, KeyError, TypeError):
            log.warning("Invalid emotion metadata omitted from joined voice")
            data = None
        if data is not None:
            cues.extend({**cue, "start": cue["start"] + offset, "end": cue["end"] + offset} for cue in data["cues"])
        offset += identity(part)[1]
    return write_emotions(wav, cues) if cues else wav
