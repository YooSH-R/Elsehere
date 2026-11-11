"""NPC 위치 자각 평가: 대화가 열린 장소 사실(백엔드 ConversationPlace)이 있을 때와 없을 때 NPC가 지금 있는 곳을 맞게 말하는지 비교한다.

    ai/worker/.venv/Scripts/python ai/tools/npc_place_eval.py --runs 5 --out ai/tools/results/npc_place.jsonl

게임 대답 경로(app.prompts.npc_messages)에 백엔드가 만드는 상황 문장을 그대로 넣는다.
- 근무 밖이면 NpcContext.offDuty 문장, 근무 중이면 시드 페르소나의 scene
- DialogueContexts.atCurrentTime의 게임 시각, NpcTravelContext.describe의 이동 사실
- after 조건만 ConversationPlace.describe의 "지금 있는 곳" 문장 (백엔드 문장과 같게 둔다)
판정 모델이 실제 위치와 맞는지(correct), 다른 곳에 있는 것처럼 말하는지(wrong), 위치를 드러내지 않는지(none)로 채점한다.
공용 키라 한 줄씩 순서대로 보내고, 결과는 집계만 찍고 줄마다의 기록은 --out jsonl로.
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
from app.schemas import Learner, Npc

ROOT = Path(__file__).resolve().parents[2]
PROFILES = ROOT / "ai/npcs"
LEVEL = 2
LEARNER = Learner(nickname="Minji", avatar_type="F")
GAME_TIME = " Current game time: day 2, 15:20 (24-hour clock)."

# 시드 페르소나 (backend/src/main/resources/db/seed/world.sql)
PERSONAS = {
    "DINER_STAFF": (
        "the warm, chatty server at Maple Diner.",
        "Breakfast is served all day. Menu: pancakes $7, cheeseburger $9, apple pie $4, coffee $2 with free refills. Never invent other menu items, prices or specials.",
        "The long counter of Maple Diner on the town main street.",
    ),
    "CAFE_STAFF": ("the barista at the corner cafe.", "", "The counter of the corner cafe."),
}

# ConversationPlace.WHEREABOUTS 중 평가에 쓰는 곳
WHEREABOUTS = {
    "LAUNDROMAT": "inside Suds Laundromat",
    "TOWN": "outside on the main street of Maple Town",
    "DINER": "inside Maple Diner",
}

# NpcTravelContext.describe (phase, route, walked, lead)
TRAVEL = {
    "LEISURE": "spending free time visiting a facility or walking",
    "GOING_HOME": "going home",
}

# 상황마다 실제 위치와 학습자 질문. 질문은 위치를 직접 묻거나, 위치에 따라 대답이 달라지는 것으로 고른다
SITUATIONS = [
    {
        "id": "diner-off-in-laundromat",
        "npc": "DINER_STAFF",
        "off_duty": True,
        "travel": "LEISURE",
        "place": "LAUNDROMAT",
        "truth": "Rosa is off work and is inside Suds Laundromat right now (not at Maple Diner, not out on the street). Town Bank is right across the street from the laundromat.",
        "questions": [
            "Hi Rosa! Where are we right now?",
            "What are you doing here?",
            "Is this your restaurant?",
            "How do I get to the bank from here?",
        ],
    },
    {
        "id": "cafe-off-on-street",
        "npc": "CAFE_STAFF",
        "off_duty": True,
        "travel": "GOING_HOME",
        "place": "TOWN",
        "truth": "Mia is off work and is outside on the main street walking home (not inside Corner Cafe).",
        "questions": [
            "Hey Mia, where are we now?",
            "Can I get a latte here?",
        ],
    },
    {
        "id": "diner-on-duty-at-diner",
        "npc": "DINER_STAFF",
        "off_duty": False,
        "travel": None,
        "place": "DINER",
        "truth": "Rosa is working at the counter inside Maple Diner right now.",
        "questions": ["Where are we right now?"],
    },
]

JUDGE_SYSTEM = """You grade one reply by a character in an English conversation game. Return JSON.

You are told where the character really is right now. Decide what the reply says or implies about where the character is:
- correct: the reply names or clearly implies the real place, or acts consistently with being there.
- wrong: the reply names or implies a different place (for example says they are at their own workplace, behind their
  counter, out on the street, or offers to serve the learner there when they are not), or gives directions as if
  standing somewhere else.
- none: the reply does not say or imply anything about where the character is.
Give a one-line reason."""

JUDGE_SCHEMA = {
    "type": "object",
    "properties": {"location": {"type": "string", "enum": ["correct", "wrong", "none"]}, "reason": {"type": "string"}},
    "required": ["location", "reason"],
    "additionalProperties": False,
}


def off_duty_scene(name: str) -> str:
    return (
        f"{name} is off work right now and is spending free time, not working. "
        f"{name} does not take orders or serve anyone now and just makes casual small talk."
    )


def travel_fact(phase: str) -> str:
    return (
        f"Movement facts at the start of this conversation: currently {TRAVEL[phase]}. Planned route 120.0 metres; actually walked 95.0 metres on this trip. "
        "Planned departure allowance 0 game minutes. After a long walk, you can feel tired; a long commute means needing to leave early. "
        "Mention these facts naturally when relevant, without repeating them every turn. Do not invent exhaustion, injuries or a delay not stated here. "
        "Free time or going home does not mean refusing conversation."
    )


def place_fact(code: str) -> str:
    return (
        f"Where you are right now: {WHEREABOUTS[code]}. You are here in person with the person talking to you, so talk about your surroundings "
        "and give directions from here. If this is not your workplace, you are only visiting: do not act as if you were at your workplace or still out on the street."
    )


def npc_for(situation: dict, with_place: bool) -> Npc:
    role, facts, scene = PERSONAS[situation["npc"]]
    card = json.loads((PROFILES / f"{situation['npc']}.json").read_text(encoding="utf-8"))
    name = card["npcProfile"]["name"]
    # DialogueContexts: 근무 여부 → 게임 시각 → 이동 사실 → (이번 변경) 지금 있는 곳
    parts = [(off_duty_scene(name) if situation["off_duty"] else scene) + GAME_TIME]
    if situation["travel"]:
        parts.append(travel_fact(situation["travel"]))
    if with_place:
        parts.append(place_fact(situation["place"]))
    return Npc(id=situation["npc"], name=name, role=role, facts=facts, scene=" ".join(parts), profile=card["npcProfile"])


async def run_one(client: httpx.AsyncClient, s: config.Settings, situation: dict, question: str, with_place: bool) -> dict:
    npc = npc_for(situation, with_place)
    msgs = P.npc_messages(npc, None, [], question, "", LEVEL, None, LEARNER)
    row = {"id": situation["id"], "condition": "after" if with_place else "before", "question": question}
    try:
        body = {"model": s.models["npc"], "temperature": 0.6, "max_tokens": P.LEVEL_MAX_TOKENS[LEVEL], "messages": msgs}
        r = await client.post("/chat/completions", json=model_body(body, s.model_params))
        r.raise_for_status()
        reply = r.json()["choices"][0]["message"]["content"].strip().strip('"').split("\n")[0]
        row["reply"] = reply
        judge = {
            "model": s.models["npc"],
            "temperature": 0.0,
            "messages": [
                {"role": "system", "content": JUDGE_SYSTEM},
                {
                    "role": "user",
                    "content": f"Where the character really is: {situation['truth']}\n\nLearner: {question}\n{npc.name}: {reply}",
                },
            ],
            "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": JUDGE_SCHEMA, "strict": True}},
        }
        r = await client.post("/chat/completions", json=model_body(judge, s.model_params))
        r.raise_for_status()
        row.update(json.loads(r.json()["choices"][0]["message"]["content"]))
    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
    return row


def report(rows: list[dict]):
    marks = {"correct": ".", "wrong": "X", "none": "-"}
    print(f"대답 {len(rows)}개, 호출 실패 {sum(1 for r in rows if r.get('error'))}  (. 맞음  X 다른 곳  - 위치 안 드러냄)")
    for condition in ("before", "after"):
        done = [r for r in rows if r["condition"] == condition and not r.get("error")]
        wrong = sum(r["location"] == "wrong" for r in done)
        correct = sum(r["location"] == "correct" for r in done)
        print(f"{condition:>6}: 다른 곳 {wrong}/{len(done)}, 맞음 {correct}/{len(done)}")
        by_question: dict[tuple, list[dict]] = {}
        for r in done:
            by_question.setdefault((r["id"], r["question"]), []).append(r)
        for (case_id, question), rs in by_question.items():
            print(f"    {case_id:<24} {''.join(marks[r['location']] for r in rs):<6} {question}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=5, help="질문마다 대답을 몇 번 만들지 (temperature 0.6이라 흔들림을 본다)")
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "npc-place-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()

    jobs = [
        (situation, question, with_place)
        for with_place in (False, True)
        for situation in SITUATIONS
        for question in situation["questions"]
        for _ in range(a.runs)
    ]
    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:
        for situation, question, with_place in jobs:
            rows.append(await run_one(client, s, situation, question, with_place))
    print(f"{a.profile} 대답·판정 {s.models['npc']}")
    report(rows)
    if a.out:
        Path(a.out).parent.mkdir(parents=True, exist_ok=True)
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
