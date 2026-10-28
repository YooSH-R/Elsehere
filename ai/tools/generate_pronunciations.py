"""사물 단어 발음 음성(WAV)을 AI 워커 TTS로 만든다. 파일이 없는 단어만 만들고, 이미 있는 파일은 그대로 둔다.

    ai/.venv/Scripts/python ai/tools/generate_pronunciations.py

TTS 모델이 있는 AI 워커가 떠 있어야 한다. 주소는 AI_WORKER_BASE_URL(기본 http://localhost:8020),
토큰은 워커와 같은 AI_WORKER_TOKEN 환경 변수로 받는다.

원본: backend/src/main/resources/content/object-words.json (Unity가 내보낸 공용 단어 목록)
결과: backend/src/main/resources/content/pronunciation/{key}.wav
서버는 시작할 때 이 폴더에 파일이 있는 단어에 발음 키를 넣고, WORLD-04로 파일을 그대로 내려준다.
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORDS = ROOT / "backend/src/main/resources/content/object-words.json"
OUTPUT = ROOT / "backend/src/main/resources/content/pronunciation"
VOICE = "af_heart"  # NPC 기본 목소리와 같다
SPEED = 1.0


def synthesize(base_url: str, token: str, text: str) -> bytes:
    body = json.dumps({"text": text, "voice": VOICE, "speed": SPEED}).encode("utf-8")
    request = urllib.request.Request(
        f"{base_url}/v1/tts",
        data=body,
        headers={"Content-Type": "application/json", "X-Internal-Token": token},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--force", action="store_true", help="이미 있는 파일도 다시 만든다")
    args = parser.parse_args()

    base_url = os.environ.get("AI_WORKER_BASE_URL", "http://localhost:8020").rstrip("/")
    token = os.environ.get("AI_WORKER_TOKEN", "")
    words = json.loads(WORDS.read_text(encoding="utf-8"))["words"]
    OUTPUT.mkdir(parents=True, exist_ok=True)

    created = skipped = 0
    for word in words:
        file = OUTPUT / f"{word['key']}.wav"
        if file.exists() and not args.force:
            skipped += 1
            continue
        try:
            file.write_bytes(synthesize(base_url, token, word["text"]))
        except urllib.error.HTTPError as error:
            print(f"{word['key']}: 워커 응답 {error.code} {error.read().decode('utf-8', 'replace')}", file=sys.stderr)
            return 1
        except urllib.error.URLError as error:
            print(f"워커에 연결하지 못했습니다 ({base_url}): {error.reason}", file=sys.stderr)
            return 1
        created += 1
        print(f"{word['key']}.wav ← {word['text']}")

    print(f"새로 만듦 {created}개, 이미 있어 건너뜀 {skipped}개 (전체 {len(words)}개)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
