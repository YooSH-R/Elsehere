"""설치된 로컬 Kokoro 모델로 안내 음성·음소·감정을 한 번 제작한다. 외부 요청 없음."""

import argparse
import io
import json
import sys
import wave
from pathlib import Path

import numpy as np
import onnxruntime as ort
from kokoro_onnx import Kokoro
from kokoro_onnx.tokenizer import Tokenizer

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / "ai/worker"))
from app.speech_alignment import align_timing
from app.speech_emotion import identity, write_emotions
from app.speech_timing import phoneme_sequence, read_timing


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-dir", type=Path, required=True)
    args = parser.parse_args()
    text = "Tap 'Replay rap' and I'll play it again!"
    korean = "‘랩 다시 듣기’를 누르면 다시 들려줄게요."
    opts = ort.SessionOptions()
    opts.intra_op_num_threads = 4
    session = ort.InferenceSession(
        str(args.model_dir / "kokoro-v1.0.onnx"),
        opts,
        providers=["CPUExecutionProvider"],
    )
    engine = Kokoro.from_session(session, str(args.model_dir / "voices-v1.0.bin"))
    samples, rate = engine.create(
        text, voice="am_onyx", speed=1.1, sentence_pause=0, clause_pause=0, trim=False
    )
    assert np.isfinite(samples).all() and np.max(np.abs(samples)) < 1
    wav = io.BytesIO()
    with wave.open(wav, "wb") as writer:
        writer.setparams((1, 2, rate, 0, "NONE", "not compressed"))
        writer.writeframes((samples * 32767).astype("<i2").tobytes())
    pcm = wav.getvalue()
    expected = phoneme_sequence(Tokenizer().phonemize(text, lang="en-us"))
    aligned = align_timing(pcm, expected)
    duration = len(samples) / rate
    packaged = write_emotions(
        aligned,
        [{"start": 0, "end": duration, "emotion": "warm_smile", "intensity": 0.5}],
    )
    assert identity(packaged) == identity(pcm)
    folder = Path(__file__).parent / "LittleMung_JohnPark_Male/ReplayGuide"
    folder.mkdir(exist_ok=True)
    (folder / "Guide.json").write_text(
        json.dumps(
            {
                "english": text,
                "korean": korean,
                "voice": "am_onyx",
                "speed": 1.1,
                "duration": duration,
                "emotion": "warm_smile",
                "pcmSha256": identity(pcm)[0],
                "timeline": read_timing(packaged),
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    target = (
        ROOT
        / "frontend/UnityClient/Assets/Audio/Characters/JohnParkDialogue/ReplayGuide.bytes"
    )
    target.write_bytes(packaged)
    review = ROOT / "artifacts/JohnParkRhythm/ReplayGuide.wav"
    review.write_bytes(packaged)
    print(
        json.dumps(
            {
                "duration": duration,
                "phonemes": len(read_timing(packaged)["phonemes"]),
                "output": str(target),
            }
        )
    )


if __name__ == "__main__":
    main()
