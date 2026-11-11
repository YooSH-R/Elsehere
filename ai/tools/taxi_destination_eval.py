"""택시 목적지 판정 평가: 의도 판정(/v1/judge)이 택시 기사에게 한 말에서 출발할 곳을 문맥으로 고르는지 잰다.
테스트셋은 ai/tools/taxi_destination_eval/cases.json (지시문을 다듬을 때 보는 개발셋).

    ai/worker/.venv/Scripts/python ai/tools/taxi_destination_eval.py --out ai/tools/results/taxi_destination.jsonl

상태(의도·장소 이름표)는 백엔드 TaxiRide(backend/.../dialogue/service/TaxiRide.java)가 보내는 것과 같게 둔다. 한쪽을 고치면 같이 고친다.
공용 키라 한 줄씩 순서대로 보내고, 결과는 집계와 틀린 줄만 찍는다.
"""

import argparse
import asyncio
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))
import httpx

from app import config
from app import prompts as P
from app.llm import model_body
from app.schemas import Line, Npc, State
from app.services import clean_intent

ROOT = Path(__file__).resolve().parents[2]
CASES = Path(__file__).resolve().parent / "taxi_destination_eval/cases.json"

# 백엔드 시드의 택시 기사 Joe (backend/src/main/resources/db/seed/world.sql)
DRIVER = Npc(
    id="TAXI_DRIVER",
    name="Joe",
    role="the friendly driver of a yellow taxi in town.",
    scene="A yellow taxi stopped by the curb on the town main street.",
)

# TaxiRide.STATE와 같은 값. 택시는 북쪽 빵집·공원·오락실·생활용품점·아이스크림 트럭에는 가지 않는다
PLACES = [
    "Corner Cafe",
    "the hotel",
    "the drugstore",
    "the learner's home",
    "the post office",
    "Maple Diner",
    "Town Bank",
    "Suds Laundromat",
    "Bloom Flowers (flower shop)",
    "Main St. Barber (barber shop)",
    "the police station",
]
STATE = {
    "id": "taxi_ride",
    "desc": "You drive the taxi and the learner is your passenger in the back seat. You drive only to places in this town. "
    "Maple Burger is the burger shop labelled Maple Diner in the destination list.",
    "intents": [
        {
            "id": "go_to",
            "desc": "Tells you a place to drive them to now, or agrees to go to a place, even in a few words, in Korean or as a"
            " plain order. Also changing to another place during the ride. Words like 'there', 'that place', 'yes' or 'the first"
            " one' mean the place just talked about in the conversation.",
        },
        {
            "id": "chat",
            "desc": "Anything else: greetings, small talk, asking where a place is, how long the ride takes or how much it costs,"
            " places they went before or do not want to go, and talk during the ride that does not ask for a new place.",
        },
    ],
    "recent_lines": 6,
    "slots": {
        "option": {
            "slot": "destination",
            "labels": PLACES,
            "prompt": "the place in this town they want to be driven to now",
        }
    },
}


def destination(out: dict) -> str:
    """택시가 출발할 곳: 목적지를 말한 줄(go_to)의 장소, 그 밖에는 none (백엔드 TaxiRide.destination과 같은 규칙)."""
    return out["slots"]["destination"] if out["id"] == "go_to" or "go_to" in out["ids"] else "none"


async def judge(client: httpx.AsyncClient, s: config.Settings, case: dict) -> dict:
    state = State.model_validate(STATE)
    history = [Line(who=who, text=text) for who, text in case["history"]]
    body = {
        "model": s.models["intent"],
        "temperature": 0.0,
        "max_tokens": 200,
        "messages": P.intent_messages(DRIVER, state, history, case["text"]),
        "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": P.intent_schema(state), "strict": True}},
    }
    row = {"id": case["id"], "want": case["destination"]}
    try:
        r = await client.post("/chat/completions", json=model_body(body, s.model_params))
        r.raise_for_status()
        row["got"] = destination(clean_intent(json.loads(r.json()["choices"][0]["message"]["content"]), state))
    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
    return row


def report(rows: list[dict]):
    done = [r for r in rows if "got" in r]
    ok = sum(r["got"] == r["want"] for r in done)
    missed = sum(r["want"] != "none" and r["got"] == "none" for r in done)
    wrong_place = sum(r["want"] != "none" and r["got"] not in ("none", r["want"]) for r in done)
    false_start = sum(r["want"] == "none" and r["got"] != "none" for r in done)
    print(f"{len(rows)}줄, 호출 실패 {len(rows) - len(done)}")
    print(f"  출발할 곳까지 맞음 {ok}/{len(done)}")
    print(f"  가야 하는데 안 감 {missed}, 다른 곳으로 감 {wrong_place}, 가면 안 되는데 감 {false_start}")
    for r in done:
        if r["got"] != r["want"]:
            print(f"  틀림 {r['id']:<26} 정답 {r['want']}  판정 {r['got']}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "taxi-destination-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()
    cases = json.loads(CASES.read_text(encoding="utf-8"))["cases"]
    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:
        for case in cases:
            rows.append(await judge(client, s, case))
    print(f"{a.profile} 판정 {s.models['intent']}")
    report(rows)
    if a.out:
        Path(a.out).parent.mkdir(parents=True, exist_ok=True)
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
