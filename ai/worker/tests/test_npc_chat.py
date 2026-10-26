"""ai/tools/npc_chat.py: 터미널에서 NPC와 주고받는 도구의 크레딧 계산과 한 턴 처리 (가짜 공급자, 모델 호출 없음)."""

import asyncio
import sys
from pathlib import Path

import httpx

from app import config
from app.config import Settings
from app.llm import LLM
from app.schemas import Npc
from conftest import stream

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_chat

LUNA_PRICES = {"gpt-6-luna": {"input": 1000, "output": 5000, "unit": "크레딧"}}
BARISTA = Npc(id="CAFE_STAFF", name="Emma", role="the barista at Maple Cafe.")
GREETING = "Hi! What can I get for you?"


def usage_item(model: str, input_tokens: int, output_tokens: int, **extra) -> dict:
    return {"step": "npc", "model": model, "input_tokens": input_tokens, "output_tokens": output_tokens, **extra}


def say_once(provider, text: str) -> tuple["npc_chat.Turn", "npc_chat.Conversation"]:
    """기본 구성(gms-luna, NPC는 gpt-6-luna)으로 가짜 공급자에게 한 턴을 말한다. 대체 모델 없이."""
    settings = Settings(token="t", base_url="http://fake/v1", api_key="x", call_timeout_s=1.0, retry_pause_s=0.0, fallback={})

    async def run():
        llm = LLM(settings, httpx.MockTransport(provider.handler))
        try:
            conversation = npc_chat.Conversation(llm, settings, BARISTA, level=1, prices=LUNA_PRICES, greeting=GREETING)
            return await conversation.say(text), conversation
        finally:
            await llm.aclose()

    return asyncio.run(run())


def test_turn_cost_multiplies_input_and_output_tokens_by_their_credit_prices():
    cost = npc_chat.turn_cost([usage_item("gpt-6-luna", 1000, 200)], LUNA_PRICES)

    assert cost.input_tokens == 1000
    assert cost.output_tokens == 200
    assert cost.credits == 2.0


def test_turn_cost_has_no_credits_when_a_model_has_no_credit_price():
    prices = {**LUNA_PRICES, "openai/gpt-oss-20b": {"input": 0.075, "output": 0.3}}  # 달러 단가 (크레딧 아님)
    items = [usage_item("gpt-6-luna", 1000, 200), usage_item("openai/gpt-oss-20b", 50, 5), usage_item("unknown-model", 10, 1)]

    cost = npc_chat.turn_cost(items, prices)

    assert cost.input_tokens == 1060
    assert cost.credits is None


def test_turn_cost_marks_turns_that_sent_the_same_request_twice():
    # 늦은 호출에 같은 요청을 하나 더 보내면 진 쪽 토큰은 기록에 없지만 GMS는 둘 다 차감한다
    plain = npc_chat.turn_cost([usage_item("gpt-6-luna", 1000, 200)], LUNA_PRICES)
    hedged = npc_chat.turn_cost([usage_item("gpt-6-luna", 1000, 200, hedged=True)], LUNA_PRICES)

    assert plain.hedged is False
    assert hedged.hedged is True


def test_say_returns_the_npc_reply_with_its_cost_and_remembers_both_lines(provider):
    provider.answers["npc"] = lambda body: stream(["Sure! ", "One iced latte."])  # 사용량: 입력 300, 출력 20

    turn, conversation = say_once(provider, "one iced latte please")

    assert turn.reply == "Sure! One iced latte."
    assert turn.error is None
    assert turn.models == ["gpt-6-luna"]
    assert (turn.cost.input_tokens, turn.cost.output_tokens) == (300, 20)
    assert turn.cost.credits == 0.4  # 300 × 0.001 + 20 × 0.005
    assert [(line.who, line.text) for line in conversation.history] == [
        ("npc", GREETING),
        ("me", "one iced latte please"),
        ("npc", "Sure! One iced latte."),
    ]


def test_say_reports_the_provider_error_and_keeps_the_history_for_a_retry(provider):
    provider.answers["npc"] = lambda body: httpx.Response(500, json={"error": "down"})

    turn, conversation = say_once(provider, "one iced latte please")

    assert turn.reply == ""
    assert turn.error == "provider_unavailable"
    assert [(line.who, line.text) for line in conversation.history] == [("npc", GREETING)]


def test_a_gms_key_alone_is_enough_because_only_the_npc_role_is_called(monkeypatch):
    # gms-luna 구성은 음성 인식(Groq) 키도 검사하지만 이 도구는 NPC 대답만 부른다
    for name in ("LLM_API_KEY", "GMS_API_KEY", "GROQ_API_KEY", "GROK_API_KEY", "LLM_PROFILE"):
        monkeypatch.setenv(name, "")  # 개발자 환경의 키가 섞이지 않게 비워 둔다 (테스트가 끝나면 되돌림)
    monkeypatch.setenv("GMS_KEY", "gms-test-key")
    monkeypatch.setenv("AI_WORKER_TOKEN", "t")
    monkeypatch.setattr(config, "load_dotenv", lambda *args, **kwargs: None)

    npc_chat.fill_unused_provider_keys("gms-luna")
    settings = Settings.from_env()

    assert settings.models["npc"] == "gpt-6-luna"
    assert settings.api_key == "gms-test-key"
