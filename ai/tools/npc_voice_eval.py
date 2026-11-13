"""NPC 개성 구분 평가: 같은 곳에서 일하는 동료들에게 같은 말을 걸고, 대화만 보고 누가 말했는지 맞히는 비율을 잰다.

    ai/worker/.venv/Scripts/python ai/tools/npc_voice_eval.py --runs 2 --out ai/tools/results/npc_voice_after.jsonl
    ai/worker/.venv/Scripts/python ai/tools/npc_voice_eval.py --cards <이전 카드 폴더> --out ai/tools/results/npc_voice_before.jsonl

- 대화: 동료 묶음(카페·호텔·은행·경찰서·드럭스토어)의 NPC마다 같은 학습자 말 네 줄로 한 대화를 만든다.
  게임 대답 경로(app.prompts.npc_messages, 난이도 2)와 백엔드 시드의 페르소나(world.sql)를 그대로 쓴다.
- 맞히기: 판정 모델은 동료들의 성격 특징(traits)과 어조(tone)만 보고, NPC 대사만 남긴 대화가 누구 것인지 고른다.
  두 칸은 이전 카드와 지금 카드가 같으므로, 맞히는 비율이 오르면 대답에 인물이 더 드러난 것이다.
- 말버릇: 지금 카드의 말버릇이 대답에 나온 비율과, 한 대화에서 같은 말버릇을 두 번 쓴 대화 수. 매 턴 나오면 틀처럼 들린다.
- 되풀이: 앞 대답에서 한 질문을 거의 그대로 다시 물은 대화 수. 앞 턴을 기억하지 못하는 모델에서 늘어난다.
- 대답은 게임 대답 스트림과 같이 표정 지시문을 붙여 만들고, 표정 표시는 빼고 남긴다.
공용 키라 한 줄씩 순서대로 보내고, 결과는 집계만 찍고 대화마다의 기록은 --out jsonl로.
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
from app.npc_emotion import EMOTION_PROMPT
from app.schemas import Learner, Line, Npc

ROOT = Path(__file__).resolve().parents[2]
WORLD_SQL = ROOT / "backend/src/main/resources/db/seed/world.sql"
FACE_MARKER = re.compile(r"\[\[[^\]]*\]\]")

# 같은 곳에서 같은 상황으로 일하는 동료들. 장소·상황이 같아 대사만으로는 장소를 단서로 쓸 수 없다
GROUPS = {
    "cafe": ["CAFE_STAFF", "CAFE_BARISTA", "CAFE_SERVER"],
    "hotel": ["HOTEL_STAFF", "HOTEL_RECEPTIONIST", "HOTEL_BELLHOP", "HOTEL_NIGHT_MANAGER"],
    "bank": ["BANK_STAFF", "TOWN_EVENING_WALKER", "TOWN_SHOPPER"],
    "police": ["POLICE_OFFICER", "POLICE_ROOKIE", "POLICE_SERGEANT"],
    "drugstore": ["SHOP_STAFF", "SHOP_PHARMACIST", "SHOP_NIGHT_CLERK"],
}
# 장소와 상관없이 성격이 드러날 만한 학습자 말. 인사 → 잡담 → 실수 걱정 → 일 이야기
LEARNER_LINES = [
    "Hi, how's it going?",
    "I just moved here. Is it always this quiet?",
    "Sorry, my English is not good.",
    "What's your favorite thing about your job?",
]
LEVEL = 2
LEARNER = Learner(nickname="Minji", avatar_type="F")

JUDGE_SYSTEM = """Several coworkers work at the same place. You get a short description of each and one conversation in
which one of them talked with a customer. Decide which coworker said the Staff lines, from how they talk and react.
Return JSON: "npc" is the id of the coworker, "reason" one short sentence."""


def personas() -> dict[str, tuple[str, str, str]]:
    """백엔드 시드(world.sql)의 NPC 페르소나 {code: (role, facts, scene)}. 게임이 대답 요청에 보내는 값과 같다."""
    sql = WORLD_SQL.read_text(encoding="utf-8")
    found = {}
    for code, raw in re.findall(r"'([A-Z_]+)',\s*'[^']*',\s*'[A-Z_]+',\s*'[^']*',\s*'(\{.*?\})'", sql, flags=re.S):
        persona = json.loads(raw.replace("''", "'"))
        found[code] = (persona["role"], persona["facts"], persona["scene"])
    return found


def card(cards: Path, npc_id: str) -> dict:
    return json.loads((cards / f"{npc_id}.json").read_text(encoding="utf-8"))["npcProfile"]


def described(profile: dict) -> str:
    """판정 모델에 보여 줄 인물 설명. 이전 카드와 지금 카드에 똑같이 있는 칸(성격 특징·어조)만 쓴다."""
    traits = "; ".join(f"{t['name']}: {t['description']}" for t in profile["personality"]["traits"])
    return f"{traits} Tone: {profile['speechStyle']['tone']}."


def key(text: str) -> str:
    return " ".join("".join(ch for ch in text.lower() if ch.isalnum() or ch == " ").split())


def catchphrases_in(reply: str, phrases: list[str]) -> list[str]:
    said = f" {key(reply)} "
    return [phrase for phrase in phrases if f" {key(phrase)} " in said]


async def complete(client: httpx.AsyncClient, s: config.Settings, body: dict) -> str:
    r = await client.post("/chat/completions", json=model_body({"model": s.models["npc"], **body}, s.model_params))
    r.raise_for_status()
    return r.json()["choices"][0]["message"]["content"].strip()


async def conversation(client, s, npc: Npc, model: str | None = None) -> list[str]:
    """model을 주면 그 모델로 대답한다 (client는 그 모델의 공급자)."""
    history: list[Line] = []
    replies = []
    for text in LEARNER_LINES:
        msgs = P.npc_messages(npc, None, history, text, "", LEVEL, None, LEARNER)
        msgs[0] = {**msgs[0], "content": msgs[0]["content"] + "\n\n" + EMOTION_PROMPT}  # 게임 대답 스트림(app.npc)과 같게
        body = {"temperature": 0.6, "max_tokens": P.LEVEL_MAX_TOKENS[LEVEL] + 64, "messages": msgs}
        reply = await complete(client, s, {**body, "model": model} if model else body)
        reply = " ".join(FACE_MARKER.sub(" ", reply.strip('"').split("\n")[0]).split())  # 게임이 읽고 자막으로 보여 주는 말만
        replies.append(reply)
        history += [Line(who="me", text=text), Line(who="npc", text=reply)]
    return replies


async def guess(client, s, coworkers: dict[str, str], replies: list[str]) -> dict:
    people = "\n".join(f"- {npc_id}: {about}" for npc_id, about in coworkers.items())
    convo = "\n".join(f"Customer: {text}\nStaff: {reply}" for text, reply in zip(LEARNER_LINES, replies))
    schema = {
        "type": "object",
        "properties": {"npc": {"type": "string", "enum": list(coworkers)}, "reason": {"type": "string"}},
        "required": ["npc", "reason"],
        "additionalProperties": False,
    }
    body = {
        "temperature": 0.0,
        "max_tokens": 150,
        "messages": [
            {"role": "system", "content": JUDGE_SYSTEM},
            {"role": "user", "content": f"Coworkers:\n{people}\n\nConversation:\n{convo}"},
        ],
        "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": schema, "strict": True}},
    }
    return json.loads(await complete(client, s, body))


def repeats_catchphrase(used_per_reply: list[list[str]]) -> bool:
    """한 대화의 대답마다 쓴 말버릇 목록에서, 앞 대답에 쓴 말버릇을 다시 쓴 적이 있는지."""
    seen: set[str] = set()
    for used in used_per_reply:
        if seen & set(used):
            return True
        seen |= set(used)
    return False


QUESTION_FILLER = {"you", "your", "the", "a", "to", "is", "are", "do", "what", "i", "it", "of", "in", "for", "and", "here", "so"}


def question_words(question: str) -> set[str]:
    return set(re.findall(r"[a-z']+", question.lower())) - QUESTION_FILLER


def repeated_questions(replies: list[str]) -> list[int]:
    """앞 대답에서 한 질문을 거의 그대로 다시 물은 대답의 번호. 질문(?로 끝나는 문장)끼리 핵심 단어가 절반 넘게 겹치면 되풀이로 본다."""
    asked: list[set[str]] = []
    found = []
    for i, reply in enumerate(replies):
        questions = [question_words(q) for q in re.split(r"(?<=[.!?])\s+", FACE_MARKER.sub(" ", reply)) if q.strip().endswith("?")]
        if any(len(q & p) >= 0.5 * max(1, len(q | p)) for q in questions for p in asked):
            found.append(i)
        asked += questions
    return found


def report(rows: list[dict]):
    done = [r for r in rows if not r.get("error")]
    right = sum(r["guess"] == r["npc"] for r in done)
    replies = [reply for r in done for reply in r["replies"]]
    with_phrase = sum(bool(used) for r in done for used in r["catchphrases"])
    repeated = sum(repeats_catchphrase(r["catchphrases"]) for r in done)
    asked_again = sum(bool(repeated_questions(r["replies"])) for r in done)
    print(f"대화 {len(rows)}개, 호출 실패 {len(rows) - len(done)}")
    print(f"  누가 말했는지 맞힘 {right}/{len(done)}")
    print(f"  말버릇이 나온 대답 {with_phrase}/{len(replies)}, 같은 말버릇을 다시 쓴 대화 {repeated}/{len(done)}")
    print(f"  앞에서 한 질문을 다시 물은 대화 {asked_again}/{len(done)}")
    for group, members in GROUPS.items():
        marks = " ".join(f"{m}:{''.join('o' if r['guess'] == m else 'x' for r in done if r['npc'] == m)}" for m in members)
        print(f"  {group:<10} {marks}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=2, help="NPC마다 대화를 몇 번 만들지 (temperature 0.6이라 흔들림을 본다)")
    ap.add_argument("--cards", default=str(ROOT / "ai/npcs"), help="대답에 쓸 인격 카드 폴더 (이전 카드와 비교할 때 바꿈)")
    ap.add_argument("--judge-cards", default=str(ROOT / "ai/npcs"), help="판정 모델 설명과 말버릇 집계에 쓸 카드 폴더")
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument(
        "--reply-gpu-model",
        help="대답만 GPU 서버 vLLM 모델로 만들기 (예: Qwen/Qwen3.8-27B, GUARD_GPU_BASE_URL·TOKEN 필요). 맞히기 판정은 구성의 npc 모델 그대로",
    )
    ap.add_argument("--reply-model", help="대답만 같은 공급자의 다른 모델로 만들기 (예: gpt-6-luna). 채점·판정은 구성의 npc 모델 그대로")
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "npc-voice-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()
    seeds, cards, judge_cards = personas(), Path(a.cards), Path(a.judge_cards)

    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:
        reply_client = None
        if a.reply_gpu_model:
            gpu = s.providers["gpu_llm"]
            reply_client = httpx.AsyncClient(base_url=gpu["base_url"], headers={"Authorization": f"Bearer {gpu['api_key']}"}, timeout=60)
        for group, members in GROUPS.items():
            coworkers = {m: described(card(judge_cards, m)) for m in members}
            for npc_id in members:
                profile = card(cards, npc_id)
                role, facts, scene = seeds[npc_id]
                npc = Npc(id=npc_id, name=profile["name"], role=role, facts=facts, scene=scene, profile=profile)
                phrases = card(judge_cards, npc_id)["speechStyle"].get("catchphrases") or []
                for _ in range(a.runs):
                    row = {"group": group, "npc": npc_id}
                    try:
                        row["replies"] = await conversation(reply_client or client, s, npc, a.reply_gpu_model or a.reply_model)
                        row["catchphrases"] = [catchphrases_in(reply, phrases) for reply in row["replies"]]
                        judged = await guess(client, s, coworkers, row["replies"])
                        row["guess"], row["reason"] = judged["npc"], judged["reason"]
                    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
                        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
                    rows.append(row)
                    print(".", end="", flush=True)
        if reply_client:
            await reply_client.aclose()
    print()
    if a.out:
        Path(a.out).parent.mkdir(parents=True, exist_ok=True)
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")
    print(f"{a.profile} 대답 {a.reply_gpu_model or a.reply_model or s.models['npc']}, 카드 {cards}")
    report(rows)


if __name__ == "__main__":
    asyncio.run(main())
