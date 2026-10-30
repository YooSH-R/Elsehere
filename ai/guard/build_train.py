"""무례 판정 LoRA 학습셋 만들기. 합성 문장과 실제 플레이 문장을 train_lora.py 입력 형식으로 합친다.

    python ai/guard/build_train.py --places <prototype/mvp_web/places> --synth <synth/train_v1.jsonl> \\
        --real <source_rows.json> --agent <agent_labels.json> --at-play <at_play_v2_dev.jsonl> --adjudicated <adjudicate_labels.json> \\
        --holdout dev_own_v1_1.jsonl --holdout test_locked_v1_1.jsonl --holdout test_unsure_v1_1.jsonl --out train.jsonl

실제 플레이 문장의 라벨: 가이드만 본 에이전트 라벨과 게임 당시 LLM 판정이 같으면 그 값, 갈리면 사람이 블라인드로 판정한 값.
사람이 '모르겠음'으로 둔 문장과 개발셋·테스트셋(--holdout)과 같은 문장은 넣지 않는다. 실제 플레이 문장은 --real-weight만큼 무겁게 센다.
문장은 찍지 않고 개수만 찍는다.
"""

import argparse
import json
import re
from collections import Counter
from pathlib import Path


def norm(text: str) -> str:
    return re.sub(r"\s+", " ", text.strip().lower())


def read_jsonl(path: str) -> list[dict]:
    return [json.loads(line) for line in Path(path).expanduser().read_text(encoding="utf-8").splitlines() if line.strip()]


def read_json(path: str):
    return json.loads(Path(path).expanduser().read_text(encoding="utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--places", required=True, help="NPC 정의가 든 장소 json 폴더")
    ap.add_argument("--synth", required=True)
    ap.add_argument("--real", required=True, help="실제 플레이 문장(id, text, prev, npc)")
    ap.add_argument("--agent", required=True, help="에이전트 라벨 {id: {rude}}")
    ap.add_argument("--at-play", required=True, help="게임 당시 판정 jsonl {id, guard}")
    ap.add_argument("--adjudicated", required=True, help="갈린 문장의 사람 판정 {id: {rude}}")
    ap.add_argument("--holdout", action="append", default=[], help="겹치면 안 되는 평가셋 jsonl (여러 번)")
    ap.add_argument("--real-weight", type=float, default=2.0)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()

    npcs = {}
    for path in sorted(Path(a.places).expanduser().glob("*.json")):
        npc = read_json(path)["npc"]
        npcs[npc["id"]] = {k: npc.get(k, "") for k in ("id", "name", "role", "facts", "scene")}
    held = {norm(row["text"]) for path in a.holdout for row in read_jsonl(path)}

    agent, adjudicated = read_json(a.agent), read_json(a.adjudicated)
    at_play = {row["id"]: row.get("guard") for row in read_jsonl(a.at_play)}
    out, skipped = [], Counter()
    for row in read_json(a.real):
        mine, then = agent[row["id"]]["rude"], at_play.get(row["id"])
        rude = mine if then is not None and mine == int(bool(then)) else (adjudicated.get(row["id"]) or {}).get("rude")
        if rude not in (0, 1):
            skipped["real_undecided"] += 1
            continue
        out.append(
            {"text": row["text"], "prev": row["prev"] or "", "rude": rude, "npc": npcs[row["npc"]], "weight": a.real_weight, "from": "real"}
        )
    for row in read_jsonl(a.synth):
        out.append(
            {
                "text": row["text"],
                "prev": row["prev"] or "",
                "rude": int(row["rude"]),
                "npc": npcs[row["npc"]],
                "weight": 1.0,
                "from": "synth",
            }
        )

    kept, seen = [], set()
    for row in out:
        key = norm(row["text"])
        if key in held:
            skipped["holdout_overlap"] += 1
        elif key in seen:
            skipped["duplicate"] += 1
        else:
            seen.add(key)
            kept.append(row)
    Path(a.out).expanduser().write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in kept), encoding="utf-8")
    print(
        json.dumps(
            {"written": len(kept), "by": Counter((r["from"], r["rude"]) for r in kept).most_common(), "skipped": skipped},
            ensure_ascii=False,
            default=str,
        )
    )


if __name__ == "__main__":
    main()
