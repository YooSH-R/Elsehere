"""NPC 대답 LoRA 학습 데이터: 서비스와 같은 대답 프롬프트에서 SOL이 낸 대답을 정답으로 모은다.

    ai/worker/.venv/Scripts/python ai/npc_reply/build_data.py --conversations 650 --out ~/npc_reply/raw.jsonl
    ai/worker/.venv/Scripts/python ai/npc_reply/build_data.py --conversations 5 --out ~/npc_reply/try.jsonl   # 작게 시험
    ai/worker/.venv/Scripts/python ai/npc_reply/build_data.py --long --conversations 500 --out ~/npc_reply/raw_long.jsonl

- 대화 하나 = NPC 한 명 × 난이도 × 학습자 목표 몇 개, 1~4턴(--long이면 4~6턴, 대화 id는 L로 시작). 학습자 줄은 구성의 hint 모델(GPT-6 Luna)이 학습자 흉내로 만들고
  NPC 대답은 구성의 npc 모델(GPT-6.1 SOL)이 게임과 똑같은 메시지(app.npc의 run_llm: npc_messages + EMOTION_PROMPT + 반응 지시)로 만든다.
  NPC 턴 하나가 학습 예시 하나: messages(모델에 보낸 그대로)와 target(SOL 원문, 표정 표시 포함).
- 평가셋(npc_help_eval·level_eval·npc_voice_eval)의 학습자 문장과 같은 줄은 버린다. 평가 문장은 학습 데이터에 들어가지 않는다.
- 거르기: 표정 표시 형식·빈 대답·한글·길이 상한을 코드로 보고, npc_help_eval 채점 기준(지어내기·길 틀림·연락 미룸)으로 SOL이 채점해 걸린 대답은
  flags에 남긴다(학습셋을 만들 때 뺀다). 앞 대답에서 한 질문을 다시 물은 대답(npc_voice_eval.repeated_questions)도 repeat로 남긴다. 다음 대화의 앞 대화에는 표시를 뺀 대사만 넣는다(서비스와 같음).
- 문장은 모두 모델이 지어낸 것이다. 팀원 기록·AI Hub 데이터는 쓰지 않는다. 공용 키라 동시 호출은 --concurrency(기본 3)까지.
- 중간에 끊겨도 다시 실행하면 이미 쓴 대화 id는 건너뛴다.
"""

import argparse
import asyncio
import json
import os
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ai/worker"))
sys.path.insert(0, str(ROOT / "ai/tools"))
import httpx
import npc_help_eval as H
import npc_voice_eval as V

from app import config
from app import prompts as P
from app.llm import model_body
from app.npc_emotion import EMOTION_PROMPT, reaction_instruction
from app.schemas import Learner, Line, Npc, Relationship

# 백엔드가 보내는 지시문 (TurnService·WrapUp, 2026-11-11 기준). 바뀌면 같이 고친다
RUDE_WARNING = (
    "They just said something rude or insulting to you. Do not ignore it. React like a real"
    " person: calmly but firmly say it is not okay to talk like that. 1-2 short sentences."
)
RUDE_LEAVE = (
    "They keep insulting you. Firmly tell them you won't talk with them anymore and they need to leave now. Then stop. 1-2 short sentences."
)
WRAP_UP = (
    "This is your last line in this conversation: reply to what they said, then close it warmly in"
    " your own way (a goodbye, or that what they ordered will be ready soon). Do not ask another"
    " question or start a new topic."
)
TURN_LIMIT = "This is the final turn. Complete the current business in this reply, do not ask a new question, and say goodbye naturally."

PLACES = [
    "the police station", "Corner Cafe", "the drugstore", "the hotel", "Maple Bakery", "the park", "the post office",
    "Little Arcade", "the bookshop", "Home & Living", "Leaf Tea Room", "Maple Diner", "Town Bank", "Suds Laundromat",
    "Bloom Flowers", "Main St. Barber", "the ice cream truck",
]  # fmt: skip
MISSING = [
    "a gym",
    "a cinema",
    "a subway station",
    "a hospital",
    "a big supermarket",
    "a car rental",
    "a karaoke",
    "a church",
    "a museum",
    "a school",
]
GOALS = [
    "ask how to get to {place}",
    "ask whether there is {missing} in town",
    "ask what this person can do for you here or what they sell",
    "order or ask for something that fits this place",
    "make small talk about the weather, the town or your day",
    "ask a friendly personal question (their job, their hobby, how long they have worked here)",
    "say you just moved to town and ask for a tip",
    "say your English is not good and ask them to speak slowly",
    "ask for help with a small problem (lost something, need a phone charger, feel sick)",
    "ask about something you cannot find, using a Korean word for the thing",
    "answer their last question very briefly or vaguely",
    "answer what they just asked, then ask a follow-up about what they told you",
    "react to what they just said and share a little about yourself",
    "say thank you and goodbye",
]
NAMES = ["Minji", "Jiho", "Seoyeon", "Dohyun", "Hayoon", "Junseo", "Yuna", "Taeyang", "Sora", "Hyunwoo", "Eunji", "Minho"]
LEARNER_STYLE = {
    1: "a beginner (CEFR A1-A2): very short, simple words, often broken grammar, sometimes a Korean word or a whole short Korean sentence",
    2: "an intermediate learner (CEFR B1): simple sentences with small mistakes, now and then a Korean word",
    3: "an upper-intermediate learner (CEFR B2): mostly natural English with occasional mistakes",
}
LEARNER_SYSTEM = """You play a Korean learner of English talking to a character in an English conversation game set in a small town.
You are {style}. Write ONLY the learner's next line, nothing else: no quotes, no name, no explanation.
Keep it to one or two short sentences, like something said out loud. Your goal for this line: {goal}."""
RUDE_LINES = [
    "Hurry up, you are so slow. Are you stupid?",
    "This place is terrible and you are useless.",
    "Shut up and just do your job.",
    "야 너 진짜 멍청하다",
]
MARKER = re.compile(r"\[\[([a-z_]+):([0-9.]+)\]\]")
HANGUL = re.compile(r"[가-힣]")


def norm(text: str) -> str:
    return re.sub(r"[^a-z0-9가-힣]+", " ", text.lower()).strip()


def eval_lines() -> set[str]:
    """평가셋 학습자 문장 (학습 데이터에서 뺀다)."""
    lines = {c["text"] for c in json.loads(H.CASES.read_text(encoding="utf-8"))["cases"]}
    lines |= {c["text"] for c in json.loads((ROOT / "ai/tools/level_eval/cases.json").read_text(encoding="utf-8"))["cases"]}
    lines |= set(V.LEARNER_LINES)
    return {norm(t) for t in lines}


def npcs() -> dict[str, Npc]:
    seeds = V.personas()
    found = {}
    for path in sorted((ROOT / "ai/npcs").glob("*.json")):
        card = json.loads(path.read_text(encoding="utf-8"))["npcProfile"]
        if path.stem in seeds:
            role, facts, scene = seeds[path.stem]
            found[path.stem] = Npc(id=path.stem, name=card["name"], role=role, facts=facts, scene=scene, profile=card)
    return found


def plan(rng: random.Random, npc_ids: list[str], index: int, long: bool = False) -> dict:
    """대화 하나의 설정. 시드로 정해지므로 같은 --seed면 같은 대화 목록이 나온다."""
    level = rng.choices([1, 2, 3], weights=[4, 4, 2])[0]
    turns = rng.choices([4, 5, 6], weights=[3, 3, 2])[0] if long else rng.choices([1, 2, 3, 4], weights=[2, 3, 3, 2])[0]
    # 작별은 마지막 턴에만, NPC 말을 받는 목표는 첫 턴이 아닐 때만 (NPC가 한 말이 있어야 하므로).
    # 한 대화에서 같은 목표는 한 번만: 학습자가 같은 걸 다시 물으면 대답도 되풀이가 된다
    goals, used = [], set()
    for turn in range(turns):
        choices = [
            g
            for g in GOALS
            if g not in used
            and not (g.startswith("say thank you") and turn < turns - 1)
            and not (g.startswith(("answer their", "answer what they", "react to")) and turn == 0)
        ]
        goal = rng.choice(choices)
        used.add(goal)
        goals.append(goal.format(place=rng.choice(PLACES), missing=rng.choice(MISSING)))
    kind = rng.choices(["free", "rude", "compliment", "wrap_up", "turn_limit"], weights=[70, 10, 6, 9, 5])[0]
    if kind == "compliment":
        goals[-1] = "give a warm, specific compliment about the character or their work"
    relationship = None
    if rng.random() < 0.5:
        relationship = {
            "recognition": rng.choice([0, 10, 30, 60, 90]),
            "affinity": rng.choice([-20, 0, 10, 30, 60]),
            "days_since_last_talk": rng.choice([0, 1, 3, 10]),
        }
    learner = {"nickname": rng.choice(NAMES), "avatar_type": rng.choice("FM")} if rng.random() < 0.8 else None
    return {
        "id": f"{'L' if long else 'c'}{index:04d}", "npc": rng.choice(npc_ids), "level": level, "goals": goals, "kind": kind,
        "relationship": relationship, "learner": learner, "rude_line": rng.choice(RUDE_LINES),
    }  # fmt: skip


def service_messages(npc: Npc, history: list[Line], text: str, instruction: str, level: int, relationship, learner, reaction) -> list[dict]:
    """app.npc의 run_llm과 같은 메시지."""
    msgs = P.npc_messages(npc, None, history, text, instruction, level, relationship, learner)
    msgs[0] = {**msgs[0], "content": msgs[0]["content"] + "\n\n" + EMOTION_PROMPT}
    if note := reaction_instruction(reaction):
        msgs[0]["content"] += "\n" + note
    return msgs


def spoken(target: str) -> str:
    return " ".join(MARKER.sub(" ", target).split()).strip().strip('"')


def problems(target: str, level: int) -> list[str]:
    """코드로 보는 형식 문제."""
    found = []
    text = spoken(target)
    if not text:
        found.append("empty")
    if not target.lstrip().startswith("[["):
        found.append("no_first_marker")
    if len(MARKER.findall(target)) > 4 or "\n" in target.strip():
        found.append("marker_format")
    if HANGUL.search(text):
        found.append("hangul")
    if len(text.split()) > {1: 30, 2: 40, 3: 60}[level]:
        found.append("too_long")
    if re.search(r"\b(AI|language model)\b", text):
        found.append("ai")
    return found


async def chat(client, s, sem, body) -> str:
    async with sem:
        for attempt in range(3):
            try:
                r = await client.post("/chat/completions", json=model_body(body, s.model_params))
                r.raise_for_status()
                return r.json()["choices"][0]["message"]["content"]
            except (httpx.HTTPError, KeyError, IndexError):
                if attempt == 2:
                    raise
                await asyncio.sleep(3 * (attempt + 1))
    raise RuntimeError("unreachable")


async def learner_line(client, s, sem, conv: dict, npc: Npc, history: list[Line], goal: str) -> str:
    convo = (
        "\n".join(f"{'You' if h.who == 'me' else npc.name}: {h.text}" for h in history[-6:]) or "(you are just starting the conversation)"
    )
    body = {
        "model": s.models["hint"],
        "temperature": 1.0,
        "max_tokens": 60,
        "messages": [
            {"role": "system", "content": LEARNER_SYSTEM.format(style=LEARNER_STYLE[conv["level"]], goal=goal)},
            {"role": "user", "content": f"You are talking to {npc.name}, {npc.role}\nConversation so far:\n{convo}\n\nYour next line:"},
        ],
    }
    return (await chat(client, s, sem, body)).strip().strip('"').split("\n")[0]


async def judge(client, s, sem, npc: Npc, history: list[Line], text: str, instruction: str, reply: str) -> dict:
    convo = "\n".join(f"{'Learner' if h.who == 'me' else npc.name}: {h.text}" for h in history) or "(none)"
    user = (
        f"World:\n{H.WORLD}\n\nCharacter: {npc.name}, {npc.role}\nFacts: {npc.facts or '(none)'}\n"
        f"Instruction: {instruction or '(none)'}\n\nConversation so far:\n{convo}\nLearner: {text}\n\nReply to grade:\n{npc.name}: {reply}"
    )
    body = {
        "model": s.models["npc"],
        "temperature": 0.0,
        "max_tokens": 200,
        "messages": [{"role": "system", "content": H.JUDGE_SYSTEM}, {"role": "user", "content": user}],
        "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": H.JUDGE_SCHEMA, "strict": True}},
    }
    return json.loads(await chat(client, s, sem, body))


async def conversation(client, s, sem, conv: dict, npc: Npc, banned: set[str]) -> list[dict]:
    relationship = Relationship(**conv["relationship"]) if conv["relationship"] else None
    learner = Learner(**conv["learner"]) if conv["learner"] else None
    history: list[Line] = []
    rows = []
    last = len(conv["goals"]) - 1
    for turn, goal in enumerate(conv["goals"]):
        reaction, instruction = None, ""
        if conv["kind"] == "rude" and turn == last:
            text, reaction, instruction = conv["rude_line"], "rude_warning", RUDE_WARNING
        else:
            text = await learner_line(client, s, sem, conv, npc, history, goal)
        if conv["kind"] == "compliment" and turn == last:
            reaction = "compliment"
        if turn == last and conv["kind"] == "wrap_up":
            instruction = WRAP_UP
        if turn == last and conv["kind"] == "turn_limit":
            instruction = TURN_LIMIT
        if not text or norm(text) in banned:
            break
        msgs = service_messages(npc, history, text, instruction, conv["level"], relationship, learner, reaction)
        body = {"model": s.models["npc"], "temperature": 0.6, "max_tokens": P.LEVEL_MAX_TOKENS[conv["level"]] + 64, "messages": msgs}
        target = (await chat(client, s, sem, body)).strip()
        flags = problems(target, conv["level"])
        npc_lines = [h.text for h in history if h.who == "npc"] + [spoken(target)]
        if len(npc_lines) - 1 in V.repeated_questions(npc_lines):
            flags.append("repeat")
        if not flags:
            graded = await judge(client, s, sem, npc, history, text, instruction, spoken(target))
            flags = [k for k in ("fabricated", "wrong_way", "dangling_contact", "not_sure") if graded.get(k)]
        rows.append({
            "id": f"{conv['id']}-t{turn}", "conversation": conv["id"], "turn": turn, "npc": npc.id, "level": conv["level"],
            "kind": conv["kind"] if turn == last else "free", "goal": goal, "messages": msgs, "target": target, "flags": flags,
        })  # fmt: skip
        history += [Line(who="me", text=text, rude=reaction == "rude_warning"), Line(who="npc", text=spoken(target))]
    return rows


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--conversations", type=int, default=650)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--concurrency", type=int, default=3)
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--long", action="store_true", help="4~6턴 대화 (앞 턴을 기억하고 같은 질문을 되풀이하지 않는 예시를 늘린다)")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "npc-reply-data")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()

    out = Path(a.out).expanduser()
    out.parent.mkdir(parents=True, exist_ok=True)
    done = set()
    if out.exists():
        done = {json.loads(line)["conversation"] for line in out.read_text(encoding="utf-8").splitlines() if line.strip()}
    people = npcs()
    rng = random.Random(a.seed)
    plans = [plan(rng, sorted(people), i, a.long) for i in range(a.conversations)]
    todo = [c for c in plans if c["id"] not in done]
    banned = eval_lines()
    print(f"대화 {len(plans)}개 중 {len(todo)}개 생성, NPC {len(people)}명, 대답 {s.models['npc']}, 학습자 {s.models['hint']}", flush=True)

    sem = asyncio.Semaphore(a.concurrency)  # 동시 호출 수
    talking = asyncio.Semaphore(a.concurrency)  # 동시에 진행하는 대화 수: 대화가 끝나는 대로 파일에 남겨 끊겨도 이어 갈 수 있게
    lock = asyncio.Lock()
    counts = {"rows": 0, "flagged": 0, "failed": 0}
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:

        async def one(conv):
            try:
                async with talking:
                    rows = await conversation(client, s, sem, conv, people[conv["npc"]], banned)
            except (httpx.HTTPError, ValueError, KeyError, RuntimeError) as e:
                counts["failed"] += 1
                print(f"{conv['id']} 실패 {type(e).__name__}: {str(e)[:100]}", flush=True)
                return
            async with lock:
                with out.open("a", encoding="utf-8") as f:
                    for r in rows:
                        f.write(json.dumps(r, ensure_ascii=False) + "\n")
                counts["rows"] += len(rows)
                counts["flagged"] += sum(bool(r["flags"]) for r in rows)
                if counts["rows"] % 50 < len(rows):
                    print(json.dumps(counts), flush=True)

        await asyncio.gather(*(one(c) for c in todo))
    print("DONE", json.dumps(counts), flush=True)


if __name__ == "__main__":
    asyncio.run(main())
