"""build_data.py 결과에서 걸린 대답을 빼고 학습셋·개발셋을 나눈다. 개발셋은 대화 단위로 떼어 같은 대화가 양쪽에 들어가지 않게 한다.

python ai/npc_reply/make_sets.py ~/npc_reply/raw.jsonl --out ~/npc_reply --dev-share 0.05
"""

import argparse
import collections
import json
import random
from pathlib import Path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("raw")
    ap.add_argument("--out", required=True)
    ap.add_argument("--dev-share", type=float, default=0.05)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()

    rows = [json.loads(line) for line in Path(a.raw).expanduser().read_text(encoding="utf-8").splitlines() if line.strip()]
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
        print(name, len(part), dict(collections.Counter(r["level"] for r in part)), dict(collections.Counter(r["kind"] for r in part)))
    print("걸려서 뺀 대답", len(rows) - len(kept), dict(collections.Counter(f for r in rows for f in r["flags"])))


if __name__ == "__main__":
    main()
