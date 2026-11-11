"""저장된 검수 음성에 감정 메타데이터를 붙인다. 합성·네트워크 호출은 하지 않는다."""

import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / "ai" / "worker"))
from app.speech_emotion import identity, write_emotions


def build(output: Path):
    audio = ROOT / "frontend/UnityClient/Assets/Audio/Characters/CafeEmotion"
    output.mkdir(parents=True, exist_ok=True)
    evidence = []
    for emotion, source in (
        ("Angry", "FrownConcernCalm"),
        ("Sad", "FrownConcernCalm"),
        ("Surprised", "CuriousPlayfulLaugh"),
    ):
        original = (audio / (source + ".wav")).read_bytes()
        timing = json.loads((audio / (source + ".json")).read_text(encoding="utf-8-sig"))
        digest, duration = identity(original)
        if abs(timing["duration"] - duration) > 0.02:
            raise ValueError("저장 음성과 시간표의 길이가 다릅니다.")
        if hashlib.sha256(original).hexdigest().lower() != timing["audioSha256"].lower():
            raise ValueError("시간표에 대응하는 원본 음성이 아닙니다.")
        payload = dict(version=timing["version"], provider=timing["provider"], duration=duration,
                       pcmSha256=digest, phonemes=timing.get("phonemes"), cues=timing.get("cues"))
        encoded = json.dumps(payload, separators=(",", ":")).encode()
        wav = original + b"a305" + struct.pack("<I", len(encoded)) + encoded + b"\0" * (len(encoded) % 2)
        wav = wav[:4] + struct.pack("<I", len(wav) - 8) + wav[8:]
        result = write_emotions(wav, [dict(start=0, end=duration, emotion=emotion.lower(), intensity=1)])
        if identity(result) != (digest, duration):
            raise ValueError("검수용 포장 과정에서 PCM이 바뀌었습니다.")
        (output / (emotion + ".bytes")).write_bytes(result)
        evidence.append(dict(emotion=emotion, text=timing["text"], voice=timing["voice"],
                             source=str((audio / (source + ".wav")).relative_to(ROOT)),
                             duration=duration, pcmSha256=digest))
    (output / "Source.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    print("저장 음성 3개 포장 완료. PCM·발음 시간표 보존, 새 합성 없음.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    build(parser.parse_args().output)
