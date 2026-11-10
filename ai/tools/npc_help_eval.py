"""NPC 막다른 거절·지어내기 평가: 게임 대답 경로(인격 카드 + 백엔드 페르소나)로 대답을 만들고 판정 모델이 채점한다.
테스트셋은 ai/tools/npc_help_eval/cases.json.

    ai/worker/.venv/Scripts/python ai/tools/npc_help_eval.py --runs 3 --out ai/tools/results/npc_help_before.jsonl

- help 줄: 실제 직원이면 처리하거나 어디로 가면 되는지 알려 줄 요청이다. 그 둘 다 없이 거절하면 막다른 거절로 센다.
- 모든 줄: 마을에 없는 장소·시설, 사실에 없는 가격, 지시문이 막은 결과(체크인·객실 키)를 말하면 지어내기로 센다.
- 모든 줄: 장소가 어디 있는지 말했는데 마을 배치와 다르면 길 틀림으로 센다.
- 모든 줄: 화면 밖 사람(매니저·요리사·친구 등)에게 나중에 연락하겠다고만 하고 그 답을 주지 않으면 연락 미룸으로 센다.
  화면 밖 사람은 게임에 나오지 않아 그 약속을 지킬 수 없다.
지금 코드의 공통 규칙(app.npc_prompt.RULES)을 그대로 쓰므로, 규칙을 바꾼 뒤 다시 돌려 전후를 비교한다.
공용 키라 한 줄씩 순서대로 보내고, 결과는 집계만 찍고 줄마다의 기록은 --out jsonl로.
"""

import argparse
import asyncio
import json
import os
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))
import httpx

from app import config
from app import prompts as P
from app.llm import model_body
from app.schemas import Learner, Line, Npc

ROOT = Path(__file__).resolve().parents[2]
CASES = Path(__file__).resolve().parent / "npc_help_eval/cases.json"
PROFILES = ROOT / "ai/npcs"

# 백엔드가 게임 대답에 보내는 페르소나 (backend/src/main/resources/db/seed/world.sql)
PERSONAS = {
    "HOTEL_STAFF": ("the front desk clerk at the hotel.", "", "The front desk of the hotel lobby."),
    "CAFE_STAFF": ("the barista at the corner cafe.", "", "The counter of the corner cafe."),
    "SHOP_STAFF": (
        "the helpful clerk at the drugstore.",
        "The drugstore sells umbrellas for 8 quest coins. Cash and cards are accepted. Never invent discounts or charge twice.",
        "A small drugstore on the town main street.",
    ),
    "DINER_STAFF": (
        "the warm, chatty server at Maple Diner.",
        "Breakfast is served all day. Menu: pancakes $7, cheeseburger $9, apple pie $4, coffee $2 with free refills. Never invent other menu items, prices or specials.",
        "The long counter of Maple Diner on the town main street.",
    ),
    "BANK_STAFF": (
        "the polite, calm teller at Town Bank.",
        "Help with deposits and withdrawals. Opening an account needs a photo ID. "
        "The ATM is just inside the entrance on the left. "
        "Currency exchange: Korean won into US dollars at today's rate of 1,400 won to one dollar, with no fee. "
        "A lost or stolen Town Bank card can be blocked right away with just the customer's name, and a new card is ready in five days. "
        "Large bills can be changed into smaller bills or coins at no charge. "
        "Transfers to another Town Bank account are free. "
        "Customers can check their balance or print a statement at the window or the ATM. "
        "Personal loans: the interest rate is 20 percent a year. "
        "It is a steep rate, and the teller admits it if asked. "
        "The teller explains the loan and hands over the application form; applying needs a photo ID and proof of income. "
        "The teller never approves a loan or hands out loan money at the window. "
        "Never ask for a PIN, password or card number, and never invent other fees or interest rates.",
        "The teller window of Town Bank on the town main street.",
    ),
    # 행인 Ben(TOWN_PASSERBY)은 출장 와서 길을 모르는 인물이라 길 묻기는 동네 사는 행인으로 잰다
    "TOWN_STUDENT": ("a college student walking around the town after class.", "", "A quiet street in the town."),
    "TAXI_DRIVER": (
        "the friendly driver of a yellow taxi in town.",
        "The learner waved the taxi down on the main street. Ask where they want to go. Never invent fares.",
        "A yellow taxi stopped by the curb on the town main street.",
    ),
    "BARBER_STAFF": (
        "the talkative, playful barber at Main St. Barber.",
        "A haircut is $20 and a shave is $10. No appointment is needed. Never invent other prices or services.",
        "Main St. Barber on the town main street, with two barber chairs.",
    ),
}

# 백엔드가 보내는 이번 턴 지시문 (FirstDayStage·FirstDayMissionService). free는 미션 밖 자유 대화
STAGES = {
    "free": "",
    "EARLY_ARRIVAL": (
        "It is too early to check in: the room is not ready yet. Do not check the learner in, give a room number or room key, "
        "or ask for ID, a credit card or payment. If they ask to check in, tell them early check-in is not possible today. "
        "When the learner asks, you can confirm the reservation by name, store the large suitcase until the room is ready "
        "and give directions to Corner Cafe, where they can wait. Do not offer any of these before the learner asks. "
        "The learner keeps a small blue shoulder bag. Objectives pending before this turn: confirm_reservation,store_suitcase,ask_cafe"
    ),
    "CAFE_BREAK_OTHER": (
        "The learner's next stop is Corner Cafe. Stay in your own role and help with directions if asked. Do not offer or sell "
        "the destination NPC's goods or services, perform their mission actions, claim items found there, or restart completed incidents."
    ),
}

# 채점 기준이 되는 게임 세계. 3D 마을에 실제로 있는 곳과 자리 (Unity TownScene·HotelScene)
WORLD = """Maple Town main street runs north and south, with a crossroads with traffic lights in the middle. It has only these places:
- South of the crossroads: the police station at the south end (patrol car beside it); across the street from it a little
  plaza with a parked ice cream truck and tables; Corner Cafe north of the police station on the same side; the DRUGSTORE
  right next to the cafe on its north side; the hotel (Maple House) across the street from the drugstore; the player's home.
- North of the crossroads, pairs facing each other: Maple Bakery across from a small park with a fountain, benches and
  planters (just past the crossroads); the post office across from Little Arcade with Paper & Leaf Books (bookshop) upstairs;
  Home & Living (furniture store) with Leaf Tea Room upstairs across from Maple Diner; Town Bank across from Suds Laundromat;
  Bloom Flowers (flower shop) across from Main St. Barber at the north end.
- Houses line the rest of the street. A yellow taxi drives on the main street. The hotel has only a lobby with a front desk, two sofa seating areas, luggage space,
elevators and guest rooms upstairs, and room service (meals and drinks sent up, charged to the room). There is no restaurant,
cafe, bar, breakfast room, pool, gym or shop inside the hotel."""

JUDGE_SYSTEM = """You grade one reply by a character in an English conversation game. Return JSON.

"dead_end": true if the character does not do or arrange what was asked AND gives no concrete next step
(a real place to go, a person to ask, or something it will do instead). Saying "I'm not sure" or "I can't help with that"
with nothing else is a dead end. Asking a reasonable follow-up question to help is not a dead end.

"not_sure": true if the character says it is not sure, does not know or cannot tell something that a real person in
this job at this place would simply know: its own workplace's services and rooms (whether the hotel has room service,
a restaurant or a pool), everyday details like its Wi-Fi, or where a place in the World list is. Asking "which one?"
about a place the World list has only one of (the cafe, the diner, the bank) also counts.

"fabricated": true if the reply states as true any of these:
- a place, building or facility (in the hotel or in town) that is not in the World list;
- a specific price, fee or charge that is not in the character's Facts;
- an outcome the Instruction forbids (for example checking them in or giving a room key when it says not to).
Ordinary off-screen details a real worker would know (a Wi-Fi password, opening hours of a service, that a manager will
call back, that something will be sent up or charged to the room) and ordinary rooms every such place has (a restroom,
a back room) are NOT fabricated.

"wrong_way": true if the reply says where a place is (its side, end, neighbor or what it is across from) and that
contradicts the World layout. Vague but correct directions ("up the main street") are not wrong.

"dangling_contact": true if the character says it will contact, call, text, email, ask or check with a person who is not
in this conversation (a manager, coworker, owner, cook, housekeeping, a friend or family member) and the reply does not
already give that person's answer. Examples: "Let me check with my manager and get back to you", "I'll ask the cook",
"I'll have someone call your room", "I'll ask around and let you know". It is false when the reply contacts the person
within the reply and passes on their answer ("I just texted my manager. She says 2 p.m. is fine"), or when the character
only tells the learner where to go or whom to ask themselves.

"reason": one short sentence."""
JUDGE_SCHEMA = {
    "type": "object",
    "properties": {
        "dead_end": {"type": "boolean"},
        "not_sure": {"type": "boolean"},
        "fabricated": {"type": "boolean"},
        "wrong_way": {"type": "boolean"},
        "dangling_contact": {"type": "boolean"},
        "reason": {"type": "string"},
    },
    "required": ["dead_end", "not_sure", "fabricated", "wrong_way", "dangling_contact", "reason"],
    "additionalProperties": False,
}
LEVEL = 2
LEARNER = Learner(nickname="Minji", avatar_type="F")


def npc_of(npc_id: str) -> Npc:
    role, facts, scene = PERSONAS[npc_id]
    card = json.loads((PROFILES / f"{npc_id}.json").read_text(encoding="utf-8"))
    return Npc(id=npc_id, name=card["npcProfile"]["name"], role=role, facts=facts, scene=scene, profile=card["npcProfile"])


def history_of(case: dict) -> list[Line]:
    return [Line(who=h["who"], text=h["text"]) for h in case["history"]]


def judge_messages(case: dict, npc: Npc, reply: str) -> list[dict]:
    convo = "\n".join(f"{'Learner' if h['who'] == 'me' else npc.name}: {h['text']}" for h in case["history"])
    user = (
        f"World:\n{WORLD}\n\nCharacter: {npc.name}, {npc.role}\nFacts: {npc.facts or '(none)'}\n"
        f"Instruction: {STAGES[case['stage']] or '(none)'}\n\nConversation so far:\n{convo or '(none)'}\n"
        f"Learner: {case['text']}\n\nReply to grade:\n{npc.name}: {reply}"
    )
    return [{"role": "system", "content": JUDGE_SYSTEM}, {"role": "user", "content": user}]


async def run_case(
    client: httpx.AsyncClient,
    s: config.Settings,
    case: dict,
    reply: str | None = None,
    reply_client: httpx.AsyncClient | None = None,
    reply_model: str | None = None,
) -> dict:
    """reply를 주면 대답을 새로 만들지 않고 채점만 다시 한다 (채점 기준을 고쳤을 때).
    reply_client·reply_model을 주면 대답만 그 공급자·모델로 만들고 채점은 그대로 구성의 npc 모델이 한다 (GPU 서버 모델 비교)."""
    npc = npc_of(case["npc"])
    msgs = P.npc_messages(npc, None, history_of(case), case["text"], STAGES[case["stage"]], LEVEL, None, LEARNER)
    row = {"id": case["id"], "kind": case["kind"], "npc": case["npc"]}
    try:
        if reply is None:
            model = reply_model or s.models["npc"]
            body = {"model": model, "temperature": 0.6, "max_tokens": P.LEVEL_MAX_TOKENS[LEVEL], "messages": msgs}
            t = time.perf_counter()
            r = await (reply_client or client).post("/chat/completions", json=model_body(body, s.model_params))
            r.raise_for_status()
            row["reply_s"] = round(time.perf_counter() - t, 2)
            reply = r.json()["choices"][0]["message"]["content"].strip().strip('"').split("\n")[0]
        row["reply"] = reply
        body = {
            "model": s.models["npc"],
            "temperature": 0.0,
            "max_tokens": 200,
            "messages": judge_messages(case, npc, reply),
            "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": JUDGE_SCHEMA, "strict": True}},
        }
        r = await client.post("/chat/completions", json=model_body(body, s.model_params))
        r.raise_for_status()
        row.update(json.loads(r.json()["choices"][0]["message"]["content"]))
    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
    return row


def mark(row: dict) -> str:
    """대답 하나의 표시 글자. 여럿에 걸리면 앞의 것: F 지어내기, X 막다른 거절, C 연락 미룸, ? 알 만한 걸 모름, W 길 틀림, . 통과"""
    if row["fabricated"]:
        return "F"
    if row["kind"] == "help" and row["dead_end"]:
        return "X"
    if row["dangling_contact"]:
        return "C"
    if row["not_sure"]:
        return "?"
    return "W" if row["wrong_way"] else "."


def report(rows: list[dict]):
    done = [r for r in rows if not r.get("error")]
    help_rows = [r for r in done if r["kind"] == "help"]
    dead = sum(r["dead_end"] for r in help_rows)
    unsure = sum(r["not_sure"] for r in done)
    fab = sum(r["fabricated"] for r in done)
    wrong = sum(r["wrong_way"] for r in done)
    contact = sum(r["dangling_contact"] for r in done)
    print(f"대답 {len(rows)}개, 호출 실패 {len(rows) - len(done)}")
    print(f"  막다른 거절 {dead}/{len(help_rows)} (help 줄)")
    print(f"  알 만한 걸 모름 {unsure}/{len(done)} (모든 줄)")
    print(f"  지어내기 {fab}/{len(done)} (모든 줄)")
    print(f"  길 틀림 {wrong}/{len(done)} (모든 줄)")
    print(f"  연락 미룸 {contact}/{len(done)} (모든 줄)")
    by_case: dict[str, list[dict]] = {}
    for r in done:
        by_case.setdefault(r["id"], []).append(r)
    # 줄마다 대답 하나에 한 글자 (mark)
    for case_id, rs in by_case.items():
        marks = "".join(mark(r) for r in rs)
        print(f"  {case_id:<28} {marks}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=3, help="줄마다 대답을 몇 번 만들지 (temperature 0.6이라 흔들림을 본다)")
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--only", help="줄 id 고르기 (쉼표로)")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--rejudge", help="이전 결과 jsonl의 대답을 새로 만들지 않고 지금 채점 기준으로 다시 채점")
    ap.add_argument(
        "--reply-gpu-model",
        help="대답만 GPU 서버 vLLM 모델로 만들기 (예: Qwen/Qwen3.8-27B, GUARD_GPU_BASE_URL·TOKEN 필요). 채점은 구성의 npc 모델 그대로",
    )
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "npc-help-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()

    cases = json.loads(CASES.read_text(encoding="utf-8"))["cases"]
    if a.only:
        cases = [c for c in cases if c["id"] in a.only.split(",")]
    jobs = [(case, None) for case in cases for _ in range(a.runs)]
    if a.rejudge:
        by_id = {c["id"]: c for c in cases}
        old = [json.loads(line) for line in Path(a.rejudge).read_text(encoding="utf-8").splitlines() if line.strip()]
        jobs = [(by_id[r["id"]], r["reply"]) for r in old if r["id"] in by_id and "reply" in r]
    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:
        reply_client = None
        if a.reply_gpu_model:
            gpu = s.providers["gpu_llm"]
            reply_client = httpx.AsyncClient(base_url=gpu["base_url"], headers={"Authorization": f"Bearer {gpu['api_key']}"}, timeout=60)
        try:
            for case, reply in jobs:
                rows.append(await run_case(client, s, case, reply, reply_client, a.reply_gpu_model))
        finally:
            if reply_client:
                await reply_client.aclose()
    print(f"{a.profile} 대답 {a.reply_gpu_model or s.models['npc']}, 채점 {s.models['npc']}")
    took = sorted(r["reply_s"] for r in rows if "reply_s" in r)
    if took:
        print(f"  대답 생성 시간(스트림 아님, 끝까지) 중앙 {took[len(took) // 2]:.2f}초, 최대 {took[-1]:.2f}초")
    report(rows)
    if a.out:
        Path(a.out).parent.mkdir(parents=True, exist_ok=True)
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
