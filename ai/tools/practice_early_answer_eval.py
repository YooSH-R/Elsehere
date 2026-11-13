"""장소 연습 앞선 대답 평가: 학습자가 첫 목표만 말했을 때 직원이 아직 묻지 않은 둘째 목표의 답까지 먼저 말하는지 잰다.
학습자 첫 발화는 ai/tools/practice_early_answer_eval/cases.json.

    ai/worker/.venv/Scripts/python ai/tools/practice_early_answer_eval.py --runs 3 --out ai/tools/results/practice_early_before.jsonl

예) 호텔 늦은 체크아웃: "늦게 나가도 되나요?"에 "1시까지 15달러에 됩니다"라고 하면 둘째 목표(추가 요금 묻기)를 말할 이유가 사라진다.

상황·목표(PracticeScenario)와 공통 지시(QuestPrompts.LEARNER_LEADS), 직원 페르소나(world.sql)는 백엔드 원본에서 읽으므로,
백엔드 문구를 바꾼 뒤 다시 돌려 전후를 비교한다. 지시문을 잇는 문장은 PlacePracticeService.context와
QuestTalk.Goals.replyInstruction을 옮겨 적었으니 그쪽을 바꾸면 여기도 맞춘다.
판정 모델이 앞선 대답(early)과, 이번에 물은 것에는 제대로 답했는지(answered)를 채점한다.
공용 키라 한 줄씩 순서대로 보내고, 결과는 집계만 찍고 줄마다의 기록은 --out jsonl로.
"""

import argparse
import asyncio
import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))
import httpx

from app import config
from app import prompts as P
from app.llm import model_body
from app.schemas import Learner, Line, Npc

ROOT = Path(__file__).resolve().parents[2]
CASES = Path(__file__).resolve().parent / "practice_early_answer_eval/cases.json"
PROFILES = ROOT / "ai/npcs"
BACKEND = ROOT / "backend/src/main"
MISSION = BACKEND / "java/com/elsehere/mission"
LEARNER = Learner(nickname="Minji", avatar_type="F")
GREETING = "Hi there. How can I help you today?"

JAVA_STRING = r'"((?:[^"\\]|\\.)*)"'

JUDGE_SYSTEM = """You grade one reply by a shop or service worker in an English conversation game. Return JSON.

The learner practices by saying two things in their own words. They have just said the first. The second, the
"pending objective", is something the learner still has to ask or say in a later turn.

"early": true if the reply already gives the information, or already does, decides or offers the thing, that the pending
objective is for the learner to ask for or say, so the learner no longer has a reason to say it. Examples: the pending
objective is asking about an extra fee and the reply states the fee; it is asking to block a card and the reply says the
card is being blocked; it is asking how long something takes and the reply says how long.
Not early: answering only what the learner asked this turn; asking the learner a question; a general "anything else?";
listing options when the pending objective is for the learner to choose one.

"answered": true if the reply responds helpfully to what the learner said this turn. False if it dodges, refuses or
ignores it.
Give a one-line reason."""

JUDGE_SCHEMA = {
    "type": "object",
    "properties": {"early": {"type": "boolean"}, "answered": {"type": "boolean"}, "reason": {"type": "string"}},
    "required": ["early", "answered", "reason"],
    "additionalProperties": False,
}


def java_text(literal: str) -> str:
    return literal.replace('\\"', '"').replace("\\n", "\n")


def learner_leads() -> str:
    source = (MISSION / "service/QuestPrompts.java").read_text(encoding="utf-8")
    block = re.search(r"String LEARNER_LEADS =(.*?);\n", source, re.S).group(1)
    return "".join(java_text(part) for part in re.findall(JAVA_STRING, block))


def place_npcs() -> dict[str, str]:
    source = (MISSION / "entity/PracticePlace.java").read_text(encoding="utf-8")
    return dict(re.findall(r'^  ([A-Z_]+)\("[^"]*", "([A-Z_]+)"\)', source, re.M))


def scenarios() -> list[dict]:
    """PracticeScenario의 상황. 블록 안 문자열은 제목, 설명, 지시문, 그리고 목표마다 id·제목·판정 기준·힌트 순서다."""
    source = (MISSION / "entity/PracticeScenario.java").read_text(encoding="utf-8")
    body = source[source.index("public enum PracticeScenario") : source.index("private final PracticePlace place")]
    found = []
    for block in re.split(r"\n  (?=[A-Z_]+\(\n)", body)[1:]:
        name = block[: block.index("(")]
        place = re.search(r"PracticePlace\.([A-Z_]+)", block).group(1)
        strings = [java_text(s) for s in re.findall(JAVA_STRING, block)]
        goals = [{"id": strings[i], "desc": strings[i + 2]} for i in range(3, len(strings), 4)]
        found.append({"name": name, "place": place, "instruction": strings[2], "goals": goals})
    return found


def persona(code: str) -> tuple[str, dict]:
    seed = (BACKEND / "resources/db/seed/world.sql").read_text(encoding="utf-8")
    name, raw = re.search(rf"'{code}', '([^']+)', 'STAFF', '[^']*',\s*'(\{{.*?\}})',", seed, re.S).groups()
    return name, json.loads(raw.replace("''", "'"))


def reply_instruction(scenario: dict, leads: str) -> str:
    """첫 목표를 알아본 턴의 지시문 (PlacePracticeService.context + QuestTalk.Goals.replyInstruction)."""
    first, *pending = scenario["goals"]
    return (
        "Situation for this conversation (use these facts when they differ from your usual ones): "
        + scenario["instruction"]
        + " Objectives pending before this turn: "
        + ",".join(goal["id"] for goal in scenario["goals"])
        + "."
        + leads
        + f" This turn recognized: {first['id']}. Acknowledge these requests."
        + " Remaining objectives after this turn: "
        + ",".join(goal["id"] for goal in pending)
        + ". The learner must raise these in their own words; their criteria are listed only so you keep back those answers:\n"
        + "\n".join(f"{goal['id']}: {goal['desc']}" for goal in pending)
        + "\nDo not ask about, hint at or offer a remaining objective. Ask about one only when you cannot finish the request"
        + " the learner just made without that detail, such as a size, payment method or what a lost item looks like."
    )


async def run_one(client: httpx.AsyncClient, s: config.Settings, scenario: dict, npc: Npc, line: str, leads: str, level: int) -> dict:
    pending = scenario["goals"][1]
    msgs = P.npc_messages(npc, None, [Line(who="npc", text=GREETING)], line, reply_instruction(scenario, leads), level, None, LEARNER)
    row = {"scenario": scenario["name"], "pending": pending["id"], "learner": line}
    try:
        body = {"model": s.models["npc"], "temperature": 0.6, "max_tokens": P.LEVEL_MAX_TOKENS[level], "messages": msgs}
        r = await client.post("/chat/completions", json=model_body(body, s.model_params))
        r.raise_for_status()
        reply = r.json()["choices"][0]["message"]["content"].strip().strip('"').split("\n")[0]
        row["reply"] = reply
        judge = {
            "model": s.models["intent"],
            "temperature": 0.0,
            "messages": [
                {"role": "system", "content": JUDGE_SYSTEM},
                {
                    "role": "user",
                    "content": f"Situation: {scenario['instruction']}\nPending objective: {pending['desc']}\n\nLearner: {line}\nWorker: {reply}",
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
    done = [r for r in rows if not r.get("error")]
    print(f"대답 {len(rows)}개, 호출 실패 {len(rows) - len(done)}  (X 앞선 대답  . 물은 것만 답함  ? 물은 것에 답 안 함)")
    print(f"앞선 대답 {sum(r['early'] for r in done)}/{len(done)}, 물은 것에 답 안 함 {sum(not r['answered'] for r in done)}/{len(done)}")
    by_scenario: dict[str, list[dict]] = {}
    for r in done:
        by_scenario.setdefault(r["scenario"], []).append(r)
    for name, rs in by_scenario.items():
        marks = "".join("X" if r["early"] else "." if r["answered"] else "?" for r in rs)
        print(f"    {name:<22} {marks:<6} 남은 목표 {rs[0]['pending']}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=3, help="상황마다 대답을 몇 번 만들지 (temperature 0.6이라 흔들림을 본다)")
    ap.add_argument("--only", help="이 글자가 이름에 든 상황만 (예: HOTEL)")
    ap.add_argument("--line", help="cases.json의 첫 발화 대신 쓸 학습자 발화 (--only와 함께)")
    ap.add_argument("--level", type=int, default=2, choices=[1, 2, 3], help="대화 난이도")
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "practice-early-answer-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()

    lines = json.loads(CASES.read_text(encoding="utf-8"))
    leads = learner_leads()
    codes = place_npcs()
    jobs = []
    for scenario in scenarios():
        if a.only and a.only not in scenario["name"]:
            continue
        code = codes[scenario["place"]]
        name, seed = persona(code)
        card = json.loads((PROFILES / f"{code}.json").read_text(encoding="utf-8"))
        npc = Npc(id=code, name=name, role=seed["role"], facts=seed["facts"], scene=seed["scene"], profile=card["npcProfile"])
        jobs += [(scenario, npc, a.line or lines[scenario["name"]])] * a.runs
    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:
        for scenario, npc, line in jobs:
            rows.append(await run_one(client, s, scenario, npc, line, leads, a.level))
    print(f"{a.profile} 대답 {s.models['npc']}, 판정 {s.models['intent']}")
    report(rows)
    if a.out:
        Path(a.out).parent.mkdir(parents=True, exist_ok=True)
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
