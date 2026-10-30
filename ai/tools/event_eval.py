"""대화 사건 판정 평가: 의도 판정이 사건(칭찬·사과·잡담 등)도 고르게 하는 제안을 테스트셋으로 잰다.
테스트셋과 사건 정의는 ai/tools/event_eval/README.md.

    ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant before   지금 의도 지시문 (미션 줄의 의도만 채점)
    ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant after    사건을 더한 지시문 (사건 + 의도)

공용 키라 한 줄씩 순서대로 보내고, 재시도·대체 모델 없이 부른다. 결과는 집계만 찍고 줄마다의 기록은 --out jsonl로.
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
CASES = Path(__file__).resolve().parent / "event_eval/cases.json"
EVENTS = ["GREETING", "SMALL_TALK", "PERSONAL_QUESTION", "APOLOGY", "COMPLIMENT", "TASK_DONE", "LEAVE"]
# 협의서의 지시문 초안 그대로. 의도 지시문 끝에 붙인다
EVENTS_PROMPT = """Also list in "events" what the learner does toward the partner in this line, from:
GREETING (greets politely), SMALL_TALK (chats about something besides the business),
PERSONAL_QUESTION (asks about the partner's own life), APOLOGY (apologizes),
COMPLIMENT (compliments or warmly thanks the partner), TASK_DONE (finishes what they came for),
LEAVE (says goodbye). Usually none: return []. Do not judge rudeness here.
English mistakes, Korean words and short answers are never a problem."""

# 시드 NPC (backend/src/main/resources/db/seed/world.sql의 persona)
NPCS = {
    "TOWN_PASSERBY": Npc(
        id="TOWN_PASSERBY", name="Ben", role="a friendly local walking around the town.", scene="A quiet street in the town."
    ),
    "CAFE_STAFF": Npc(id="CAFE_STAFF", name="Mia", role="the barista at the corner cafe.", scene="The counter of the corner cafe."),
    "HOTEL_STAFF": Npc(
        id="HOTEL_STAFF", name="Jake", role="the front desk clerk at the hotel.", scene="The front desk of the hotel lobby."
    ),
    "SHOP_STAFF": Npc(
        id="SHOP_STAFF",
        name="Alex",
        role="the helpful clerk at the drugstore.",
        facts="The drugstore sells umbrellas for 8 quest coins. Cash and cards are accepted. Never invent discounts or charge twice.",
        scene="A small drugstore on the town main street.",
    ),
}

# Spring이 보내는 상태: 자유 대화는 TurnService.FREE_TALK, 미션은 FirstDayStage의 지시문·목표 + FirstDayMissionService의 덧붙임 문장
FREE_TALK = {
    "id": "free_talk",
    "desc": "Free conversation. The learner can talk about anything with you.",
    "intents": [{"id": "chat", "desc": "Anything the learner says in the conversation."}],
}
MISSION_SUFFIX = (
    " Recognize only the learner's current utterance in context. Do not credit NPC lines, quoted examples, negatives,"
    " wishes without action or future hypothetical actions. One utterance may match several intents."
)
STAGES = {
    "EARLY_ARRIVAL": (
        "It is too early to check in. You can confirm the reservation, store the large suitcase and give directions to Corner Cafe. "
        "The learner keeps a small blue shoulder bag.",
        {
            "confirm_reservation": "Confirm or identify an existing hotel reservation.",
            "store_suitcase": "Ask to store the large suitcase until check-in.",
            "ask_cafe": "Ask for a nearby cafe or directions to it.",
        },
    ),
    "CAFE_BREAK": (
        "Offer drinks and ask one follow-up about size, temperature or milk. The learner may choose any drink. "
        "They need to dine in, then explicitly rest after ending the conversation. Do not mention a lost bag yet.",
        {
            "order_drink": "Order a drink, any drink is valid.",
            "set_options": "Answer about size, temperature, milk or another drink option, including no preference.",
            "dine_in": "Say the learner will have the drink here, not take away.",
        },
    ),
    "MISSING_BAG": (
        "The large suitcase is safely stored at this hotel. The learner left their separate small shoulder bag at the cafe. "
        "They need to go back for it before checking in. Do not claim the hotel has the small bag.",
        {
            "report_left_bag": "Explain the small bag is missing or was left at the cafe.",
            "return_later": "Say they will return after looking for or retrieving the bag.",
        },
    ),
    "FIND_BAG": (
        "You found a small blue shoulder bag with a red keyring. It contains a passport wallet and a book. "
        "Ask for appearance and contents, one question at a time. Return it only after the learner describes matching appearance "
        "and contents and claims ownership. Do not invent other contents.",
        {
            "ask_lost_bag": "Ask whether a lost bag was found or request help finding it.",
            "describe_bag": "Describe this bag matching blue color AND shoulder bag type or red keyring. Wrong attributes do not count.",
            "describe_contents": "Identify the passport or passport wallet AND book inside the bag. Wrong contents do not count.",
            "confirm_ownership": "Explicitly confirm the described bag is theirs or ask to collect their identified bag.",
        },
    ),
    "RAINY_STREET": (
        "It has started raining. The learner recovered the small bag. The drugstore sells umbrellas for 8 coins; "
        "the learner has 20 quest coins. Give directions to the store marked DRUGSTORE, not the cafe or hotel. "
        "Do not sell an umbrella yourself.",
        {
            "ask_umbrella_shop": "Ask where to buy an umbrella or for a store selling one.",
            "ask_directions": "Ask or confirm directions or the location of the drugstore.",
        },
    ),
    "BUY_UMBRELLA": (
        "Sell one umbrella for exactly 8 quest coins. Balance before purchase is 20. Ask how they want to pay (cash/card both accepted). "
        "Purchase completes only when price, purchase intent and payment choice are all covered. Never change the price or charge twice.",
        {
            "ask_price": "Ask or confirm the umbrella price.",
            "buy_umbrella": "Explicitly decide to buy an umbrella.",
            "choose_payment": "Choose cash, card or another payment method offered by the clerk.",
        },
    ),
    "CHECK_IN": (
        "Check-in time has arrived. The learner recovered the small bag and passport, bought an umbrella and has 12 quest coins left. "
        "The large suitcase is stored at this hotel. Confirm the reservation, check them in and return the suitcase on request. "
        "Never ask them to fetch the bag again.",
        {
            "found_bag": "Say they returned or found their bag.",
            "request_checkin": "Ask to check in now.",
            "show_reservation": "Identify or confirm their reservation, name or booking.",
            "collect_suitcase": "Ask to collect the stored large suitcase.",
        },
    ),
}


def load_cases() -> list[dict]:
    return json.loads(CASES.read_text(encoding="utf-8"))["cases"]


def state_of(name: str) -> State:
    if name == "free_talk":
        return State.model_validate(FREE_TALK)
    instruction, goals = STAGES[name]
    return State(id=name.lower(), desc=instruction + MISSION_SUFFIX, intents=[{"id": goal, "desc": desc} for goal, desc in goals.items()])


def messages(variant: str, case: dict) -> list[dict]:
    history = [Line(who="npc", text=case["last_npc"])]
    msgs = P.intent_messages(NPCS[case["npc"]], state_of(case["state"]), history, case["text"])
    if variant == "after":
        msgs[-1] = {**msgs[-1], "content": msgs[-1]["content"] + "\n" + EVENTS_PROMPT}
    return msgs


def schema(variant: str, state: State) -> dict:
    out = P.intent_schema(state)
    if variant == "after":
        out = {
            **out,
            "properties": {**out["properties"], "events": {"type": "array", "items": {"type": "string", "enum": EVENTS}}},
            "required": [*out["required"], "events"],
        }
    return out


def clean_events(raw: object) -> list[str]:
    """목록 밖 값은 버리고 중복은 하나로 (clean_intent가 의도 목록 밖 값을 버리는 것과 같음)."""
    got = raw.get("events") if isinstance(raw, dict) else None
    return [e for e in dict.fromkeys(got) if isinstance(e, str) and e in EVENTS] if isinstance(got, list) else []


def score(rows: list[dict]) -> dict:
    """rows: 케이스에 got_events(after만)·got_intents(미션만)를 붙인 것. 호출이 실패한 줄(error)은 빼고 센다."""
    done = [r for r in rows if not r.get("error")]
    out = {"n": len(rows), "failed": len(rows) - len(done)}
    judged = [r for r in done if "got_events" in r]
    if judged:
        per_event = {}
        for e in EVENTS:
            tp = sum(1 for r in judged if e in r["events"] and e in r["got_events"])
            fp = sum(1 for r in judged if e not in r["events"] and e in r["got_events"])
            fn = sum(1 for r in judged if e in r["events"] and e not in r["got_events"])
            per_event[e] = {
                "tp": tp,
                "fp": fp,
                "fn": fn,
                "precision": tp / (tp + fp) if tp + fp else None,
                "recall": tp / (tp + fn) if tp + fn else None,
            }
        empty = [r for r in judged if not r["events"]]
        out["events"] = per_event
        out["exact"] = sum(1 for r in judged if set(r["events"]) == set(r["got_events"])) / len(judged)
        out["empty_lines"] = len(empty)
        out["empty_false_alarm"] = sum(1 for r in empty if r["got_events"]) / len(empty) if empty else None
    mission = [r for r in done if "got_intents" in r]
    if mission:
        out["intent_n"] = len(mission)
        out["intent_exact"] = sum(1 for r in mission if set(r["intents"]) == set(r["got_intents"])) / len(mission)
    return out


def report(s: dict, tag: str):
    def pct(v):
        return "-" if v is None else f"{v:.0%}"

    print(f"{tag}: 줄 {s['n']}, 호출 실패 {s['failed']}")
    if "events" in s:
        print(f"  사건 완전 일치 {pct(s['exact'])}, 사건 없는 줄 {s['empty_lines']}개 중 오탐 {pct(s['empty_false_alarm'])}")
        for e, m in s["events"].items():
            print(
                f"  {e:<18} 정밀도 {pct(m['precision']):>4}  재현율 {pct(m['recall']):>4}  (맞음 {m['tp']}, 잘못 잡음 {m['fp']}, 놓침 {m['fn']})"
            )
    if "intent_n" in s:
        print(f"  미션 의도 일치 {pct(s['intent_exact'])} ({s['intent_n']}줄)")


async def judge(client: httpx.AsyncClient, s: config.Settings, variant: str, case: dict, timeout: float) -> dict:
    state = state_of(case["state"])
    body = {
        "model": s.models["intent"],
        "temperature": 0.0,
        "max_tokens": 200,
        "messages": messages(variant, case),
        "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": schema(variant, state), "strict": True}},
    }
    row = dict(case)
    try:
        r = await client.post("/chat/completions", json=model_body(body, s.model_params), timeout=httpx.Timeout(timeout, connect=10.0))
        r.raise_for_status()
        raw = json.loads(r.json()["choices"][0]["message"]["content"])
        if case["state"] != "free_talk":
            row["got_intents"] = clean_intent(raw, state)["ids"]
        if variant == "after":
            row["got_events"] = clean_events(raw)
    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
    return row


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--variant", choices=["before", "after"], required=True)
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--only", help="묶음 고르기 (쉼표로): clear,none,boundary,mission")
    ap.add_argument("--timeout", type=float, default=30.0)
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "event-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()

    cases = load_cases()
    if a.only:
        cases = [c for c in cases if c["group"] in a.only.split(",")]
    if a.variant == "before":  # 지금 지시문은 사건을 고르지 않으므로 의도를 채점할 미션 줄만
        cases = [c for c in cases if c["state"] != "free_talk"]
    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}
    ) as client:
        for case in cases:
            rows.append(await judge(client, s, a.variant, case, a.timeout))
    report(score(rows), f"{a.profile} {s.models['intent']} {a.variant}")
    if a.out:
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
