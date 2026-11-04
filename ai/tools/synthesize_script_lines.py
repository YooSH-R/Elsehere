"""정해진 대본 대사를 워커와 같은 Kokoro 합성(읽기 규칙·발음 사전·입 모양 타이밍 포함)으로 미리 WAV로 만든다.

서버를 거치지 않는 튜토리얼 대본 대사도 NPC 목소리로 들려주려고 Unity 에셋으로 넣는 음성을 만든다.
모델 파일은 ai/tools/prepare_kokoro.py로 받은 워커와 같은 고정 파일을 쓴다.

예:
  python ai/tools/synthesize_script_lines.py --models <모델 폴더> --voice am_fenrir --speed 0.85 \
      --out frontend/UnityClient/Assets/Audio/Dialogue/TutorialBen \
      --line "Opening=Excuse me! Could you tell me how to get to the café?"
"""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))

from app.tts import Kokoro  # noqa: E402  워커 폴더를 경로에 넣은 뒤 불러온다


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models", type=Path, required=True, help="prepare_kokoro.py로 받은 모델 폴더")
    parser.add_argument("--voice", required=True, help="NPC 목소리. ai/npcs/<NPC>.json의 voiceId")
    parser.add_argument("--speed", type=float, default=1.0, help="말 속도. 워커처럼 NPC 속도에 난이도 배율을 곱한 값")
    parser.add_argument("--out", type=Path, required=True, help="WAV를 쓸 폴더. 파일은 <이름>.bytes로 쓴다(Unity TextAsset)")
    parser.add_argument("--line", action="append", required=True, help="이름=대사. 여러 번 줄 수 있다")
    args = parser.parse_args()
    # Windows 콘솔(cp949)에서도 café 같은 대사를 그대로 찍는다.
    sys.stdout.reconfigure(encoding="utf-8")

    kokoro = Kokoro.load(
        str(args.models / "kokoro-v1.0.int8.onnx"),
        str(args.models / "voices-v1.0.bin"),
    )
    if kokoro is None or not kokoro.phoneme_timing_ready:
        raise SystemExit("Kokoro 모델을 불러오지 못했거나 입 모양 타이밍이 없습니다.")

    args.out.mkdir(parents=True, exist_ok=True)
    for spec in args.line:
        name, text = spec.split("=", 1)
        wav, duration, _ = kokoro.synth(text, args.voice, args.speed)
        (args.out / f"{name}.bytes").write_bytes(wav)
        print(f"{name}: {duration:.2f}s {text}")


if __name__ == "__main__":
    main()
