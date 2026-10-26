"""터미널에서 NPC와 대화하며 턴마다 토큰·크레딧·시간을 본다 (실제 모델 호출, 팀 공용 GMS 크레딧을 쓴다).

    ai/worker/.venv/Scripts/python ai/tools/npc_chat.py
    ... --npc npc.json       NPC 설정 파일 (app.schemas.Npc 형식). 없으면 확인용 바리스타
    ... --greeting "Hi!"     NPC의 첫 말 (대화 기록의 첫 줄). 확인용 바리스타는 기본 인사가 있다
    ... --level 2            대화 난이도 1~3 (기본 1: MVP는 초급)

서비스와 같은 NPC 대답 흐름(app.npc.reply_events)을 서버 없이 부르므로 프롬프트를 고치면 바로 대화로 확인할 수 있다.
모델 구성은 Worker와 같이 LLM_PROFILE(기본 gms-luna), 키는 GMS_KEY (환경 변수 또는 저장소 루트 .env).
크레딧은 llm_profiles.json의 단가로 계산한다. /quit 또는 입력 끝(Ctrl+Z, Enter)에서 대화 전체 합계를 보여 준다.
입력을 파이프로 넘기면 줄마다 한 턴씩 말한다: Get-Content lines.txt | ...npc_chat.py
"""

import argparse
import asyncio
import os
import sys
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

from pydantic import ValidationError

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))  # 같은 저장소의 AI 워커(app)를 서버 없이 불러 쓴다

from app import config
from app.config import Settings
from app.llm import LLM
from app.npc import reply_events
from app.schemas import Line, Npc, ReplyRequest
from app.tts import AudioStore

HISTORY_LIMIT = 40  # ReplyRequest.history 최대 길이 (프롬프트에는 그중 최근 12줄이 들어간다)

# 확인용 NPC. 서비스 NPC의 역할 설정은 S15P31A305-121에서 정한다
SAMPLE_NPC = Npc(
    id="CAFE_STAFF",
    name="Emma",
    role="the barista at Maple Cafe.",
    facts="Menu: americano $4, latte $5, cappuccino $5, muffin $3, cheesecake $5. Milk can be changed to soy or oat for free.",
    scene="Inside Maple Cafe, a cozy coffee shop. It is a sunny morning.",
)
SAMPLE_GREETING = "Hi there! Welcome to Maple Cafe. What can I get for you today?"


@dataclass
class TurnCost:
    input_tokens: int
    output_tokens: int
    credits: float | None
    hedged: bool  # 늦은 호출에 같은 요청을 하나 더 보냄: 진 쪽 토큰은 기록에 없어 실제 차감은 더 클 수 있다


def credit_price(model: str, prices: dict) -> dict | None:
    """모델의 크레딧 단가. 단가표에 없거나, 값을 모르거나(null), 크레딧이 아닌 단위(Groq 달러)면 None."""
    price = prices.get(model)
    if price is None or price.get("unit") != "크레딧" or price.get("input") is None or price.get("output") is None:
        return None
    return price


def turn_cost(usage_items: list[dict], prices: dict) -> TurnCost:
    """한 턴에 쓴 호출들의 토큰과 크레딧. GMS는 쓴 토큰 수 × 단가만큼 차감한다 (단가표는 100만 토큰당).
    크레딧 단가를 모르는 모델이 하나라도 섞이면 합계가 틀리므로 credits는 None."""
    input_tokens, output_tokens, credits, hedged = 0, 0, 0.0, False
    for item in usage_items:
        input_tokens += item["input_tokens"]
        output_tokens += item["output_tokens"]
        hedged = hedged or bool(item.get("hedged"))
        price = credit_price(item["model"], prices)
        if price is None or credits is None:
            credits = None
            continue
        credits += (item["input_tokens"] * price["input"] + item["output_tokens"] * price["output"]) / 1_000_000
    return TurnCost(input_tokens=input_tokens, output_tokens=output_tokens, credits=credits, hedged=hedged)


@dataclass
class Turn:
    reply: str
    error: str | None
    models: list[str]
    cost: TurnCost
    first_token_s: float | None
    total_s: float | None


class Conversation:
    """NPC와의 대화 하나. 서비스와 같은 NPC 대답 흐름(app.npc.reply_events)을 서버 없이 불러
    프롬프트·재시도·스트리밍이 Spring이 부를 때와 같다. 음성은 만들지 않는다."""

    def __init__(self, llm: LLM, settings: Settings, npc: Npc, level: int, prices: dict, greeting: str = ""):
        self.llm, self.settings, self.npc, self.level, self.prices = llm, settings, npc, level, prices
        self.history: list[Line] = [Line(who="npc", text=greeting)] if greeting else []
        self.audio = AudioStore()  # 음성을 만들지 않아도 대답 흐름이 받는 자리

    async def say(self, text: str, on_text: Callable[[str], None] | None = None) -> Turn:
        """학습자 말 한 줄에 NPC가 대답한다. on_text는 대답 글자 조각이 올 때마다 불린다.
        실패하면 대화 기록을 바꾸지 않아 같은 말을 다시 해 볼 수 있다."""
        request = ReplyRequest(npc=self.npc, history=self.history[-HISTORY_LIMIT:], text=text, tts=False, level=self.level)
        reply, error, usage, first_token_s, total_s = "", None, [], None, None
        async for kind, data in reply_events(self.llm, None, self.audio, self.settings, request):
            if kind == "delta" and on_text:
                on_text(data["text"])
            elif kind == "done":
                reply, usage = data["text"], data["usage"]
                first_token_s, total_s = data["first_token_s"], data["total_s"]
            elif kind == "error":
                error, usage = data["code"], data["usage"]
        if error is None:
            self.history += [Line(who="me", text=text), Line(who="npc", text=reply)]
        models = sorted({item["model"] for item in usage})
        return Turn(reply, error, models, turn_cost(usage, self.prices), first_token_s, total_s)


UNUSED_KEY = "unused-by-npc-chat"


def fill_unused_provider_keys(profile: str):
    """NPC 대답 역할이 쓰지 않는 공급자(gms 구성의 음성 인식 Groq)의 키가 없으면 자리 값을 넣는다.
    Settings.from_env는 구성의 모든 공급자 키를 검사하지만, 이 도구는 그 공급자를 부르지 않는다."""
    main_provider = config.PROFILES["profiles"][profile]["provider"]
    _, role_provider = config.profile_models(profile)
    npc_provider = role_provider.get("npc", main_provider)
    for name in set(role_provider.values()) - {npc_provider}:
        key_names = config.PROFILES["providers"][name]["key_env"]
        if not any(os.environ.get(key) for key in key_names):
            os.environ[key_names[0]] = UNUSED_KEY


def credits_text(credits: float | None) -> str:
    return "크레딧 단가 없음" if credits is None else f"{credits:.2f} 크레딧"


def describe(turn: Turn) -> str:
    """턴 하나의 사용량 한 줄."""
    parts = [f"입력 {turn.cost.input_tokens:,} · 출력 {turn.cost.output_tokens:,} 토큰", credits_text(turn.cost.credits)]
    if turn.first_token_s is not None:
        parts.append(f"첫 글자 {turn.first_token_s}초 · 전체 {turn.total_s}초")
    if turn.models:
        parts.append("모델 " + ", ".join(turn.models))
    if turn.cost.hedged:
        parts.append("같은 요청을 하나 더 보냄 (진 쪽 토큰은 빠져 실제 차감은 더 클 수 있음)")
    line = " · ".join(parts)
    return f"실패 {turn.error} (대화 기록은 그대로라 다시 말해 볼 수 있음) · {line}" if turn.error else line


def summary(turns: list[Turn]) -> str:
    """대화 전체 합계. 실패한 턴도 쓴 토큰은 합계에 넣고, 턴당 크레딧은 대답한 턴으로 나눈다.
    단가를 모르는 턴이 있으면 크레딧 합계를 내지 않는다."""
    costs = [t.cost for t in turns]
    answered = sum(1 for t in turns if t.error is None)
    failed = len(turns) - answered
    input_tokens = sum(c.input_tokens for c in costs)
    output_tokens = sum(c.output_tokens for c in costs)
    credits = None if any(c.credits is None for c in costs) else sum(c.credits for c in costs)
    per_turn = "" if credits is None or not answered else f" (턴당 {credits / answered:.2f})"
    failed_text = f" (실패 {failed})" if failed else ""
    return f"대답 {answered}턴{failed_text} · 입력 {input_tokens:,} · 출력 {output_tokens:,} 토큰 · 합계 {credits_text(credits)}{per_turn}"


async def chat(conversation: Conversation, npc_name: str):
    piped = not sys.stdin.isatty()
    turns: list[Turn] = []
    for line in conversation.history:
        print(f"{npc_name}> {line.text}")
    while True:
        try:
            text = input("나> ").strip()
        except EOFError:
            break
        if piped:  # 파이프 입력은 화면에 찍히지 않으므로 대화 기록처럼 보이게 다시 찍는다
            print(text)
        if text == "/quit":
            break
        if not text:
            continue
        print(f"{npc_name}> ", end="", flush=True)
        try:
            turn = await conversation.say(text, on_text=lambda piece: print(piece, end="", flush=True))
        except ValidationError as e:  # 너무 긴 말 등 요청 형식 오류: 대답 없이 다음 입력으로
            print(f"\n  보낼 수 없는 말입니다: {e.errors()[0]['msg']}")
            continue
        print()
        print(f"  {describe(turn)}")
        turns.append(turn)
    print(f"\n대화 합계: {summary(turns)}")


async def main():
    ap = argparse.ArgumentParser(description="터미널에서 NPC와 대화하며 턴마다 토큰·크레딧·시간을 본다")
    ap.add_argument("--npc", help="NPC 설정 JSON 파일 (app.schemas.Npc 형식). 없으면 확인용 바리스타")
    ap.add_argument("--greeting", help="NPC의 첫 말")
    ap.add_argument("--level", type=int, choices=[1, 2, 3], default=1, help="대화 난이도 (기본 1: 초급)")
    a = ap.parse_args()
    # 파이프로 넘긴 한영 혼합 문장과 안내 글이 Windows 기본 인코딩으로 깨지지 않게
    for stream in (sys.stdin, sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    npc = Npc.model_validate_json(Path(a.npc).read_text(encoding="utf-8")) if a.npc else SAMPLE_NPC
    greeting = a.greeting if a.greeting is not None else ("" if a.npc else SAMPLE_GREETING)
    config.load_dotenv()  # 구성 이름(LLM_PROFILE)도 .env에 있을 수 있어 먼저 읽는다
    os.environ.setdefault("AI_WORKER_TOKEN", "npc-chat")  # 서버로 띄우지 않으므로 Spring과 공유할 토큰은 쓰지 않는다
    profile = os.environ.get("LLM_PROFILE") or config.DEFAULT_PROFILE
    if profile in config.PROFILES["profiles"]:  # 없는 구성이면 Settings.from_env가 있는 구성 목록을 알려 준다
        fill_unused_provider_keys(profile)
    try:
        settings = Settings.from_env()
    except RuntimeError as e:  # 키가 없는 경우 등: 무엇을 넣어야 하는지 그대로 보여 준다
        sys.exit(str(e))
    prices = config.PROFILES["prices_per_1m_tokens_usd"]
    print(f"대화 상대 {npc.name} (구성 {settings.profile}, NPC 모델 {settings.models['npc']}, 난이도 {a.level}). 끝내려면 /quit")

    llm = LLM(settings)
    try:
        await chat(Conversation(llm, settings, npc, a.level, prices, greeting), npc.name)
    finally:
        await llm.aclose()


if __name__ == "__main__":
    asyncio.run(main())
