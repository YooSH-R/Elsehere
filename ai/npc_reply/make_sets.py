"""build_data.py 결과에서 걸린 대답을 빼고 학습셋·개발셋을 나눈다. 개발셋은 대화 단위로 떼어 같은 대화가 양쪽에 들어가지 않게 한다.

python ai/npc_reply/make_sets.py ~/npc_reply/raw.jsonl ~/npc_reply/raw_long.jsonl --out ~/npc_reply --dev-share 0.05

앞 대답에서 한 질문을 다시 물은 대답(repeat)은 build_data.py가 표시하기 전에 만든 파일에도 여기서 다시 확인해 뺀다.
"""

import argparse
import collections
import json
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import npc_voice_eval as V
from build_data import spoken


def mark_repeats(rows: list[dict]):
    """대화마다 턴 순서로 NPC 대답을 놓고, 앞에서 한 질문을 다시 물은 대답에 repeat를 붙인다."""
    by_conversation = collections.defaultdict(list)
    for r in rows:
        by_conversation[r["conversation"]].append(r)
    for turns in by_conversation.values():
        turns.sort(key=lambda r: r["turn"])
        for i in V.repeated_questions([spoken(r["target"]) for r in turns]):
            if "repeat" not in turns[i]["flags"]:
                turns[i]["flags"].append("repeat")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("raw", nargs="+")
    ap.add_argument("--out", required=True)
    ap.add_argument("--dev-share", type=float, default=0.05)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()

    rows = [json.loads(line) for path in a.raw for line in Path(path).expanduser().read_text(encoding="utf-8").splitlines() if line.strip()]
    mark_repeats(rows)
    kept = [r for r in rows if not r["flags"]]
    conversations = sorted({r["conversation"] for r in kept})
    random.Random(a.seed).shuffle(conversations)
    dev_ids = set(conversations[: max(1, round(len(conversations) * a.dev_share))])
    out = Path(a.out).expanduser()
    out.mkdir(parents=True, exist_ok=True)
    for name, part in (
        ("train", [r for r in kept if r["conversation"] not in dev_ids]),
        ("dev", [r for r in kept if r["conversation"] in dev_ids]),
    ):
        (out / f"{name}.jsonl").write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in part), encoding="utf-8")
        turns = collections.Counter(min(r["turn"], 4) for r in part)
        print(name, len(part), dict(collections.Counter(r["level"] for r in part)), dict(collections.Counter(r["kind"] for r in part)))
        print("  턴 번호(4는 4 이상)", dict(sorted(turns.items())))
    print("걸려서 뺀 대답", len(rows) - len(kept), dict(collections.Counter(f for r in rows for f in r["flags"])))


if __name__ == "__main__":
    main()
