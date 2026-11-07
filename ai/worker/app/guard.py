"""무례 판정 (/v1/guard). Spring은 한 턴에서 /v1/judge(의도·번역)와 동시에 부른다.
명세: docs/api/ai-worker.md"""

import asyncio
import json
import logging
import re

from . import npc_relationship
from . import prompts as P
from .guard_local import HANGUL, LocalGuard
from .llm import LLM, ProviderError, Usage, within
from .schemas import GuardRequest, Line, Npc

log = logging.getLogger("ai_worker")

GUARD_ANSWER = re.compile(r"^\W*(yes|no)\b", re.IGNORECASE)
# 한글 낱자모(ㄱ~ㅎ, ㅏ~ㅣ). GPU LoRA는 오타 낱자모("같ㅊ아")와 "ㄱㅅ"를 욕 줄임말로 읽어 무례로 보고, 프롬프트로도 고쳐지지 않았다
# (S15P31A305-604). 이런 줄은 구성의 guard 모델이 판정한다.
LOOSE_JAMO = re.compile(r"[ㄱ-ㆎ]")


def guard_result(out: str) -> dict:
    """명확한 yes/no만 판정으로 인정한다. 빈 출력·거절·다른 말은 미판정(abusive: null) — Spring은 무례로 세지 않는다.
    예전에는 빈 응답·거절을 무례로 봤다 (심한 욕설에 모델이 대답을 거절하는 일이 있어서). 그러면 공급자 고장도
    무례 누적이 되므로 나눈다. 거절이 얼마나 잦은지는 error_code "unparsed" 기록으로 따로 본다."""
    m = GUARD_ANSWER.match(out)
    if m:
        return {"abusive": m.group(1).lower() == "yes", "error": None, "error_code": None}
    if not out.strip():
        return {"abusive": None, "error": "판정 모델이 빈 답을 냄", "error_code": "empty"}
    return {"abusive": None, "error": f"yes/no가 아닌 답: {out[:60]!r}", "error_code": "unparsed"}


def guard_failure(e: BaseException) -> dict:
    """판정 호출 자체가 실패. 공급자 오류는 그 코드(provider_unavailable/provider_limit/timeout)를 그대로."""
    code = e.code if isinstance(e, ProviderError) else "unparsed"
    return {"abusive": None, "error": str(e)[:160], "error_code": code}


async def judge_rudeness(
    llm: LLM, npc: Npc, history: list[Line], text: str, usage: Usage, local: LocalGuard | None, budget_s: float
) -> dict:
    """행동 묘사를 지운 말 하나를 판정한다. 공급자 실패·시간 초과는 예외 대신 abusive: null 결과로 돌려준다.
    local(로컬 무례 판정기)이 있으면 단계적 구조: 확신 구간은 로컬이 판정하고 판정용 LLM을 부르지 않는다."""
    # 로컬 판정기를 켰을 때만 붙는 칸: 누가 판정했나·로컬 점수·로컬 실패 (LLM이 실패해도 남도록 밖에 둔다)
    trace = {"source": "llm", "score": None, "local_error": None} if local else {}

    async def decide():
        if local and HANGUL.search(text):
            prev = next((h.text for h in reversed(history) if h.who == "npc"), "")
            try:
                s = await asyncio.to_thread(local.score, text, prev)
            except Exception as e:  # 로컬이 고장 나도 판정은 LLM으로 이어 간다
                trace["local_error"] = f"{type(e).__name__}: {e}"[:160]
                log.warning(json.dumps({"event": "guard_local_error", "error": trace["local_error"]}, ensure_ascii=False), exc_info=True)
            else:
                trace["score"] = round(s, 4)
                if s >= llm.s.guard_t_hi or s <= llm.s.guard_t_lo:
                    trace["source"] = "local"
                    return {"abusive": s >= llm.s.guard_t_hi, "error": None, "error_code": None}
        # yes/no 한 단어로 받는다. JSON을 강제하면 욕설·위협에 빈 응답 → 400으로 턴이 죽었다
        out = await llm.guard_chat(P.guard_messages(npc, history, text), usage, gpu=not LOOSE_JAMO.search(text))
        return guard_result(out)

    try:
        result = await within(decide(), budget_s)
    except (ProviderError, ValueError) as e:
        result = guard_failure(e)
    return result | trace


async def guard(llm: LLM, req: GuardRequest, local: LocalGuard | None = None) -> dict:
    """affinity_delta: 무례로 판정했을 때만 바로 깎을 호감(app.npc_relationship). 아니거나 판정하지 못했으면 null."""
    usage = Usage()
    text, _ = P.strip_actions(req.text)
    if not text:  # 행동 묘사만 있었으면 모델을 부르지 않는다 (/v1/judge의 dropped와 같음)
        return {"text": "", "dropped": True, "guard": None, "affinity_delta": None, "usage": []}
    result = await judge_rudeness(llm, req.npc, req.history, text, usage, local, llm.s.guard_budget_s)
    affinity_delta = rude_affinity_delta(req.npc) if result["abusive"] is True else None
    return {"text": text, "dropped": False, "guard": result, "affinity_delta": affinity_delta, "usage": usage.items}


def rude_affinity_delta(npc: Npc) -> int:
    """요청의 인격 카드(성격 5축·행동 조각)로 이 NPC의 무례 반응 크기를 정해 감점을 계산한다. 카드가 없으면 기본 배수."""
    rules, library = npc_relationship.service_rules()
    return npc_relationship.rude_affinity_delta(npc_relationship.npc_rates(npc.profile, rules, library), rules)
