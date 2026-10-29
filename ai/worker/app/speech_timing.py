"""Carry normalized phoneme times inside a WAV RIFF chunk (a305).

The PCM stays byte-identical; Spring stores/returns the whole WAV unchanged.
No text or credentials are included. Missing timings stay missing, never guessed.
"""

import hashlib
import io
import json
import math
import struct
import wave

IPA = dict(zip("pbmfvtdsznklgwjh", "p b m f v t d s z n k l g w y h".split(), strict=True)) | {
    "a": "aa",
    "ɑ": "aa",
    "ɒ": "ao",
    "æ": "ae",
    "ʌ": "ah",
    "ə": "ax",
    "ɛ": "eh",
    "e": "eh",
    "i": "iy",
    "ɪ": "ih",
    "ᵻ": "ih",
    "u": "uw",
    "ʊ": "uh",
    "ɔ": "ao",
    "o": "ow",
    "ɜ": "er",
    "ɚ": "er",
    "ɝ": "er",
    "ɹ": "r",
    "r": "r",
    "ɾ": "dx",
    "ŋ": "ng",
    "θ": "th",
    "ð": "dh",
    "ʃ": "sh",
    "ʒ": "zh",
    "ʤ": "jh",
    "ʧ": "ch",
    "ʔ": "_",
}
PAIRS = {"aɪ": "ay", "aʊ": "aw", "eɪ": "ey", "ɔɪ": "oy", "oʊ": "ow", "əʊ": "ow", "tʃ": "ch", "dʒ": "jh"}
MARKS = "ˈˌː"


def _pcm(wav):
    with wave.open(io.BytesIO(wav)) as source:
        return source.readframes(source.getnframes()), source.getnframes() / source.getframerate()


def _write(wav, cues):
    pcm, duration = _pcm(wav)
    payload = json.dumps(
        {"version": 2, "provider": "kokoro-en-US", "duration": duration, "pcmSha256": hashlib.sha256(pcm).hexdigest(), "phonemes": cues},
        separators=(",", ":"),
    ).encode()
    chunk = b"a305" + struct.pack("<I", len(payload)) + payload + b"\0" * (len(payload) % 2)
    result = wav + chunk
    return result[:4] + struct.pack("<I", len(result) - 8) + result[8:]


def attach_timing(wav: bytes, spoken) -> bytes:
    if not spoken:
        return wav
    _, duration = _pcm(wav)
    cues, previous, index = [], 0.0, 0
    pending_stress = None
    while index < len(spoken):
        entry = spoken[index]
        symbol, start, end = entry.phoneme, float(entry.start), float(entry.end)
        index += 1
        if not all(math.isfinite(v) for v in (start, end)) or start < previous - 1e-7 or end < start or end > duration + 1e-7:
            raise ValueError("Invalid model phoneme timing")
        previous = end
        if end == start:
            continue
        if symbol in MARKS:
            if symbol == "ː" and cues:
                cues[-1]["end"] = end
            else:
                pending_stress = (pending_stress[0] if pending_stress else start, end)
            continue
        if pending_stress is not None:
            if abs(start - pending_stress[1]) < 1e-6:
                start = pending_stress[0]
            pending_stress = None
        if index < len(spoken) and symbol + spoken[index].phoneme in PAIRS:
            following = spoken[index]
            if abs(following.start - end) < 1e-6:
                symbol += following.phoneme
                end = float(following.end)
                if not math.isfinite(end) or end <= start or end > duration + 1e-7:
                    raise ValueError("Invalid diphthong timing")
                previous = end
                index += 1
        if symbol.isspace() or symbol in ';:,.!?—…"()':
            phone = "_"
        else:
            phone = PAIRS.get(symbol, IPA.get(symbol))
        if phone is None:
            raise ValueError("Unsupported Kokoro phoneme: " + symbol)
        cues.append({"phoneme": phone, "start": start, "end": min(end, duration)})
    return _write(wav, cues) if cues else wav


def read_timing(wav: bytes) -> dict | None:
    offset = 12
    while offset + 8 <= len(wav):
        tag, size = struct.unpack_from("<4sI", wav, offset)
        offset += 8
        if offset + size > len(wav):
            raise ValueError("Truncated WAV chunk")
        if tag == b"a305":
            data = json.loads(wav[offset : offset + size])
            pcm, duration = _pcm(wav)
            if data["pcmSha256"] != hashlib.sha256(pcm).hexdigest() or abs(data["duration"] - duration) > 1e-6:
                raise ValueError("Phonemes do not belong to this PCM")
            return data
        offset += size + size % 2
    return None


def join_timing(wav: bytes, parts: list[bytes]) -> bytes:
    cues, offset = [], 0.0
    for part in parts:
        timing = read_timing(part)
        # A partial timeline must not be presented as a complete reply.
        if timing is None:
            return wav
        cues.extend({**cue, "start": cue["start"] + offset, "end": cue["end"] + offset} for cue in timing["phonemes"])
        offset += timing["duration"]
    return _write(wav, cues)
