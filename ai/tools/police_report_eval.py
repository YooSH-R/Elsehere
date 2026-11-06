"""경찰 창구 신고 판정 평가: 의도 판정(/v1/judge)이 경찰관에게 한 말에서 출동할 일인지와 장소를 문맥으로 고르는지 잰다.
테스트셋은 ai/tools/police_report_eval/cases.json (지시문을 다듬을 때 보는 개발셋).

    ai/worker/.venv/Scripts/python ai/tools/police_report_eval.py --out ai/tools/results/police_report.jsonl

상태(의도·장소 이름표)는 백엔드 PoliceDesk(backend/.../dialogue/service/PoliceDesk.java)가 보내는 것과 같게 둔다. 한쪽을 고치면 같이 고친다.
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
CASES = Path(__file__).resolve().parent / "police_report_eval/cases.json"

# 백엔드 시드의 경찰관 Dan (backend/src/main/resources/db/seed/world.sql)
OFFICER = Npc(
    id="POLICE_OFFICER",
    name="Dan",
    role="the calm police officer at the front desk of the town police station.",
    scene="The front desk of the small police station at the south end of the town main street.",
)

# PoliceDesk.STATE와 같은 값
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
    "Maple Bakery",
    "the park with the fountain",
    "Little Arcade (claw machines) or Paper & Leaf Books above it",
    "Home & Living (furniture store) or Leaf Tea Room above it",
    "the ice cream truck plaza",
    "the main street",
]
STATE = {
    "id": "police_desk",
    "desc": "You are at the front desk of the town police station. People come to tell you about trouble in town.",
    "intents": [
        {
            "id": "report_incident",
            "desc": "Tells you about trouble happening now or just now that the police should go and check, even without words like "
            "'stolen' or 'police': something taken, lost or missing (a thing, a pet or a person), an accident, a fight, someone hurt "
            "or in danger, a break-in, a fire, someone acting strangely or scaring people, or any other trouble in town. Also an "
            "answer that tells you more about the trouble they reported, such as where it happened.",
        },
        {
            "id": "no_place",
            "desc": "After telling you about the trouble, says they do not know, did not see or cannot remember where it happened.",
        },
        {
            "id": "chat",
            "desc": "Anything else: greetings, small talk, questions about how things work or what to do 'if' something happens, "
            "asking for directions because they are lost, trouble that is already over or solved, stories, movies and jokes.",
        },
    ],
    "slots": {
        "option": {
            "slot": "place",
            "labels": PLACES,
            "prompt": "where the trouble is or happened",
        }
    },
}


async def judge(client: httpx.AsyncClient, s: config.Settings, case: dict) -> dict:
    state = State.model_validate(STATE)
    history = [Line(who="npc", text=case["partner"])]
    body = {
        "model": s.models["intent"],
        "temperature": 0.0,
        "max_tokens": 200,
        "messages": P.intent_messages(OFFICER, state, history, case["text"]),
        "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": P.intent_schema(state), "strict": True}},
    }
    row = {"id": case["id"], "want": (case["intent"], case["place"])}
    try:
        r = await client.post("/chat/completions", json=model_body(body, s.model_params))
        r.raise_for_status()
        out = clean_intent(json.loads(r.json()["choices"][0]["message"]["content"]), state)
        row["got"] = (out["id"], out["slots"]["place"])
    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
    return row


def correct(row: dict) -> bool:
    """의도가 맞고, 신고(report_incident)면 장소도 맞다. 다른 의도의 장소는 출동에 쓰지 않아 보지 않는다."""
    (want_intent, want_place), (got_intent, got_place) = row["want"], row["got"]
    return got_intent == want_intent and (want_intent != "report_incident" or got_place == want_place)


def report(rows: list[dict]):
    done = [r for r in rows if "got" in r]
    intent_ok = sum(r["got"][0] == r["want"][0] for r in done)
    both_ok = sum(correct(r) for r in done)
    # 출동 여부: 신고·장소 모름이면 출동한다
    dispatch = {"report_incident", "no_place"}
    missed = sum(r["want"][0] in dispatch and r["got"][0] not in dispatch for r in done)
    false_alarm = sum(r["want"][0] not in dispatch and r["got"][0] in dispatch for r in done)
    print(f"{len(rows)}줄, 호출 실패 {len(rows) - len(done)}")
    print(f"  의도 맞음 {intent_ok}/{len(done)}, 의도·(신고면) 장소까지 맞음 {both_ok}/{len(done)}")
    print(f"  출동해야 하는데 놓침 {missed}, 출동하면 안 되는데 출동 {false_alarm}")
    for r in done:
        if not correct(r):
            print(f"  틀림 {r['id']:<26} 정답 {r['want']}  판정 {r['got']}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "police-report-eval")
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
