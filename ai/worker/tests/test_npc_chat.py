"""ai/tools/npc_chat.py: 터미널에서 NPC와 주고받는 도구의 크레딧 계산과 한 턴 처리 (가짜 공급자, 모델 호출 없음).

NPC는 공통 인격 스키마의 설정 파일, 프롬프트는 app.npc_prompt (계약의 추론 요청 형식으로 만든다)."""

import asyncio
import sys
from pathlib import Path

import httpx

from app import config
from app.config import Settings
from app.llm import LLM
from conftest import load_npc_examples, stream

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_chat

LUNA_PRICES = {"gpt-6-luna": {"input": 1000, "output": 5000, "unit": "크레딧"}}
SOL_PRICES = {"gpt-6.1-sol": {"input": 20000, "output": 100000, "unit": "크레딧"}}
CAFE_ORDER = {
    "scenarioId": "cafe_order",
    "locationId": "cafe",
    "situation": "The customer has just come up to the counter.",
    "facts": ["Iced latte is on the menu."],
}
OPENING = "Hi! What can I get for you?"


def usage_item(model: str, input_tokens: int, output_tokens: int, **extra) -> dict:
    return {"step": "npc", "model": model, "input_tokens": input_tokens, "output_tokens": output_tokens, **extra}


def talk(provider, answers: list, lines: list[str]) -> tuple[list["npc_chat.Turn"], "npc_chat.Conversation", list[dict]]:
    """계약 예시 카페 점원과 첫 대사를 받고 lines를 차례로 말한다. answers[i]()가 가짜 공급자의 i번째 답이고,
    그 뒤 호출(오류 뒤 재시도 등)에는 마지막 답을 다시 만들어 준다.
    반환: (턴들, 대화, 공급자가 받은 요청 본문들). 기본 구성(gms-luna, NPC는 gpt-6.1-sol), 대체 모델 없이."""
    settings = Settings(token="t", base_url="http://fake/v1", api_key="x", call_timeout_s=1.0, retry_pause_s=0.0, fallback={})
    sent = []

    def answer(body):
        sent.append(body)
        return answers[min(len(sent), len(answers)) - 1]()

    provider.answers["npc"] = answer

    async def run():
        llm = LLM(settings, httpx.MockTransport(provider.handler))
        try:
            conversation = npc_chat.Conversation(llm, load_npc_examples()[0]["npcProfile"], CAFE_ORDER, level="A2", prices=SOL_PRICES)
            turns = [await conversation.open()]
            for line in lines:
                turns.append(await conversation.say(line))
            return turns, conversation
        finally:
            await llm.aclose()

    turns, conversation = asyncio.run(run())
    return turns, conversation, sent


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


def test_open_asks_for_the_first_line_with_only_the_system_prompt_and_remembers_it(provider):
    turns, conversation, sent = talk(provider, [lambda: stream(["Hi! ", "What can I get for you?"])], [])

    assert turns[0].reply == OPENING
    assert [m["role"] for m in sent[0]["messages"]] == ["system"]  # 첫 대사는 가짜 사용자 메시지 없이 (계약 4.1)
    assert sent[0]["messages"][0]["content"].startswith("## Rules")
    assert conversation.history == [{"speaker": "NPC", "text": OPENING}]


def test_say_sends_the_history_and_returns_the_reply_with_its_cost(provider):
    answers = [lambda: stream([OPENING]), lambda: stream(["Sure! ", "One iced latte."])]  # 가짜 사용량: 입력 300, 출력 20

    turns, conversation, sent = talk(provider, answers, ["one iced latte please"])

    turn = turns[1]
    assert turn.reply == "Sure! One iced latte."
    assert turn.error is None
    assert turn.models == ["gpt-6.1-sol"]
    assert (turn.cost.input_tokens, turn.cost.output_tokens) == (300, 20)
    assert turn.cost.credits == 8.0  # 300 × 0.02 + 20 × 0.1
    assert sent[1]["messages"][1:] == [{"role": "assistant", "content": OPENING}, {"role": "user", "content": "one iced latte please"}]
    assert conversation.history == [
        {"speaker": "NPC", "text": OPENING},
        {"speaker": "USER", "text": "one iced latte please"},
        {"speaker": "NPC", "text": "Sure! One iced latte."},
    ]


def test_say_reports_the_provider_error_and_keeps_the_history_for_a_retry(provider):
    answers = [lambda: stream([OPENING]), lambda: httpx.Response(500, json={"error": "down"})]

    turns, conversation, _ = talk(provider, answers, ["one iced latte please"])

    assert turns[1].reply == ""
    assert turns[1].error == "provider_unavailable"
    assert conversation.history == [{"speaker": "NPC", "text": OPENING}]


def test_a_gms_key_alone_is_enough_because_only_the_npc_role_is_called(monkeypatch):
    # gms-luna 구성은 음성 인식(Groq) 키도 검사하지만 이 도구는 NPC 대답만 부른다
    for name in ("LLM_API_KEY", "GMS_API_KEY", "GROQ_API_KEY", "GROK_API_KEY", "LLM_PROFILE"):
        monkeypatch.setenv(name, "")  # 개발자 환경의 키가 섞이지 않게 비워 둔다 (테스트가 끝나면 되돌림)
    monkeypatch.setenv("GMS_KEY", "gms-test-key")
    monkeypatch.setenv("AI_WORKER_TOKEN", "t")
    monkeypatch.setattr(config, "load_dotenv", lambda *args, **kwargs: None)

    npc_chat.fill_unused_provider_keys("gms-luna")
    settings = Settings.from_env()

    assert settings.models["npc"] == "gpt-6.1-sol"
    assert settings.api_key == "gms-test-key"
