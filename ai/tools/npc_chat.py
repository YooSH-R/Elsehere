"""터미널에서 NPC와 대화하며 턴마다 토큰·크레딧·시간을 본다 (실제 모델 호출, 팀 공용 GMS 크레딧을 쓴다).

    ai/.venv/Scripts/python ai/tools/npc_chat.py --npc <NPC 설정 파일> --scenario <상황 파일>
    ... --level B1           학습 수준 A1~C2 (기본 A2: MVP는 초급)

NPC 설정 파일은 공통 인격 스키마의 NpcConfiguration(NPC마다 하나), 상황 파일은 추론 계약의 scenario
({"scenarioId", "locationId", "situation", "facts"}). 프롬프트는 서비스와 같은 app.npc_prompt로 만들어
계약의 추론 요청과 같은 모양으로 보낸다: NPC가 첫 대사(NPC_OPENING)를 먼저 하고, 이후 한 줄마다 대답(USER_MESSAGE).
모델 구성은 Worker와 같이 LLM_PROFILE(기본 gms-luna), 키는 GMS_KEY (환경 변수 또는 저장소 루트 .env).
크레딧은 llm_profiles.json의 단가로 계산한다. /quit 또는 입력 끝(Ctrl+Z, Enter)에서 대화 전체 합계를 보여 준다.
입력을 파이프로 넘기면 줄마다 한 턴씩 말한다: Get-Content lines.txt | ...npc_chat.py
"""

import argparse
import asyncio
import json
import os
import sys
import time
from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))  # 같은 저장소의 AI 워커(app)를 서버 없이 불러 쓴다

from npc_configs import schema_errors

from app import config
from app.config import Settings
from app.llm import LLM, ProviderError, Usage
from app.npc_prompt import npc_messages

HISTORY_LIMIT = 20  # 계약의 conversationHistory 최대 발화 수
MAX_REPLY_TOKENS = 150  # 대답 길이는 학습 수준 규칙이 정하고, 이 값은 넘치지 않게 막는 상한
LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]


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
    """NPC와의 대화 하나. 턴마다 계약의 추론 요청과 같은 모양을 만들어 app.npc_prompt로 메시지를 만들고 모델을 부른다.
    history는 계약의 conversationHistory 항목({"speaker": "NPC"|"USER", "text"}). 음성은 만들지 않는다."""

    def __init__(self, llm: LLM, profile: dict, scenario: dict, level: str, prices: dict):
        self.llm, self.profile, self.scenario, self.level, self.prices = llm, profile, scenario, level, prices
        self.history: list[dict] = []

    async def open(self, on_text: Callable[[str], None] | None = None) -> Turn:
        """NPC가 대화를 먼저 연다 (NPC_OPENING)."""
        return await self._turn("NPC_OPENING", None, on_text)

    async def say(self, text: str, on_text: Callable[[str], None] | None = None) -> Turn:
        """학습자 말 한 줄에 NPC가 대답한다 (USER_MESSAGE). on_text는 대답 글자 조각이 올 때마다 불린다."""
        return await self._turn("USER_MESSAGE", text, on_text)

    async def _turn(self, turn_type: str, user_text: str | None, on_text: Callable[[str], None] | None) -> Turn:
        """실패하면 대화 기록을 바꾸지 않아 같은 말을 다시 해 볼 수 있다."""
        request = {
            "npcProfile": self.profile,
            "scenario": self.scenario,
            "userLevel": self.level,
            "turnType": turn_type,
            "userText": user_text,
            "conversationHistory": self.history[-HISTORY_LIMIT:],
        }
        usage, pieces, error, started = Usage(), [], None, time.perf_counter()
        try:
            async for piece in self.llm.stream("npc", "npc", npc_messages(request), usage, max_tokens=MAX_REPLY_TOKENS):
                pieces.append(piece)
                if on_text:
                    on_text(piece)
        except ProviderError as e:
            error = e.code
        total_s = round(time.perf_counter() - started, 2)
        reply = "" if error else "".join(pieces).strip()
        if error is None:
            if user_text is not None:
                self.history.append({"speaker": "USER", "text": user_text})
            self.history.append({"speaker": "NPC", "text": reply})
        first_token_s = next((item["first_token_s"] for item in usage.items if item.get("first_token_s") is not None), None)
        models = sorted({item["model"] for item in usage.items})
        return Turn(reply, error, models, turn_cost(usage.items, self.prices), first_token_s, total_s)


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


async def npc_line(npc_name: str, reply: Callable[[Callable[[str], None]], Awaitable[Turn]]) -> Turn:
    """NPC 대답 한 줄을 글자 조각이 오는 대로 찍고, 그 턴의 사용량 한 줄을 붙인다."""
    print(f"{npc_name}> ", end="", flush=True)
    turn = await reply(lambda piece: print(piece, end="", flush=True))
    print()
    print(f"  {describe(turn)}")
    return turn


async def chat(conversation: Conversation, npc_name: str):
    piped = not sys.stdin.isatty()
    turns = [await npc_line(npc_name, conversation.open)]
    while not turns[0].error:  # 첫 대사가 없으면 대답할 상대가 없다 (계약: 후속 응답의 이력은 NPC 대사로 끝남)
        try:
            text = input("나> ").strip()
        except EOFError:
            break
        if piped:  # 파이프 입력은 화면에 찍히지 않으므로 대화 기록처럼 보이게 다시 찍는다
            print(text)
        if text == "/quit":
            break
        if text:
            turns.append(await npc_line(npc_name, lambda on_text: conversation.say(text, on_text)))
    print(f"\n대화 합계: {summary(turns)}")


async def main():
    ap = argparse.ArgumentParser(description="터미널에서 NPC와 대화하며 턴마다 토큰·크레딧·시간을 본다")
    ap.add_argument("--npc", required=True, help="NPC 설정 파일 (공통 인격 스키마의 NpcConfiguration, NPC마다 하나)")
    ap.add_argument(
        "--scenario", required=True, help='상황 파일 (추론 계약의 scenario: {"scenarioId", "locationId", "situation", "facts"})'
    )
    ap.add_argument("--level", choices=LEVELS, default="A2", help="학습 수준 (기본 A2: MVP는 초급)")
    a = ap.parse_args()
    # 파이프로 넘긴 한영 혼합 문장과 안내 글이 Windows 기본 인코딩으로 깨지지 않게
    for stream in (sys.stdin, sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    npc = json.loads(Path(a.npc).read_text(encoding="utf-8"))
    problems = schema_errors(npc)  # 크레딧을 쓰기 전에 프로필이 스키마를 지키는지부터
    if problems:
        sys.exit("NPC 설정 파일이 스키마에 맞지 않습니다:\n" + "\n".join(f"{where}: {why}" for where, why in problems))
    scenario = json.loads(Path(a.scenario).read_text(encoding="utf-8"))
    name = npc["npcProfile"]["name"]
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
    print(f"대화 상대 {name} (구성 {settings.profile}, NPC 모델 {settings.models['npc']}, 학습 수준 {a.level}). 끝내려면 /quit")

    llm = LLM(settings)
    try:
        await chat(Conversation(llm, npc["npcProfile"], scenario, a.level, prices), name)
    finally:
        await llm.aclose()


if __name__ == "__main__":
    asyncio.run(main())
