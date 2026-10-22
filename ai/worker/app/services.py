"""판정·힌트·대화 종료·음성 인식. 상태는 바꾸지 않는다 (상태 전환은 Spring 규칙)."""

import asyncio
import json
import logging
import re

from . import prompts as P
from .guard_local import HANGUL, LocalGuard
from .llm import LLM, ModelOutputError, ProviderError, Usage
from .schemas import EndRequest, HintRequest, JudgeRequest, State

log = logging.getLogger("ai_worker")


def clean_intent(raw: object, state: State) -> dict:
    """모델 출력을 요청 범위 안으로 (형식 강제가 풀린 json_object 대체 모드에서도 엉뚱한 값이 나가지 않게).
    JSON 객체가 아니면 ModelOutputError — 판정에서는 의도 칸만 실패(unclear + error)로 처리된다."""
    if not isinstance(raw, dict):
        raise ModelOutputError(f"의도 판정 출력이 JSON 객체가 아님: {type(raw).__name__}")
    allowed = {i.id for i in state.intents + state.more_intents}
    got = raw.get("intents") if isinstance(raw.get("intents"), list) else [raw.get("intent")]  # 대체 모드는 intent 하나로 올 수 있음
    ids = [i for i in dict.fromkeys(i for i in got if isinstance(i, str)) if i in allowed][:3] or ["unclear"]
    # id: 첫 행동 (예전 형식과 호환), ids: 말한 순서대로 전부 — Spring은 ids를 차례로 적용한다
    out = {"id": ids[0], "ids": ids, "slots": {}, "error": None}
    s = state.slots
    if s.items:
        items = []
        for it in raw.get("items") or []:
            if not isinstance(it, dict):
                continue
            name = it.get("name")
            if name not in s.items.catalog:
                continue
            try:
                qty = int(it.get("qty") or 1)
            except (TypeError, ValueError):
                qty = 1
            items.append({"name": name, "iced": bool(it.get("iced")) and name in s.items.iced_ok, "qty": max(qty, 1)})
        out["slots"]["items"] = items
    if s.option:
        v = raw.get(s.option.slot)
        out["slots"][s.option.slot] = v if v in s.option.labels else "none"
    return out


async def within(coro, budget_s: float):
    """요청 하나의 시간 상한. call_timeout_s는 호출 한 번의 상한이라 재시도·대체 모델을 거치면 훨씬 길어진다.
    넘으면 그 작업만 timeout 오류 (끊긴 호출의 사용량은 기록되지 않을 수 있음)."""
    try:
        return await asyncio.wait_for(coro, budget_s)
    except TimeoutError:
        raise ProviderError("timeout", f"{budget_s:g}초 안에 끝나지 않음") from None


GUARD_ANSWER = re.compile(r"^\W*(yes|no)\b", re.IGNORECASE)


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
    """판정 호출 자체가 실패. 공급자 오류는 그 코드(provider_unavailable/provider_limit)를 그대로."""
    code = e.code if isinstance(e, ProviderError) else "unparsed"
    return {"abusive": None, "error": str(e)[:160], "error_code": code}


async def judge(llm: LLM, req: JudgeRequest, local: LocalGuard | None = None) -> dict:
    """번역(한국어가 섞였을 때)·무례·의도를 동시에. 하나가 실패해도 턴은 이어 간다 (의도 → unclear, 무례 → null 미판정).
    local(로컬 무례 판정기)이 있으면 단계적 구조: 확신 구간은 로컬이 판정하고 판정용 LLM을 부르지 않는다."""
    usage = Usage()
    text, had_action = P.strip_actions(req.text)
    if not text:  # 행동만 있었으면 모델을 부르지 않는다. Spring은 턴으로 세지 않음
        return {"text": "", "dropped": True, "notice": P.ACTION_ONLY_KO, "intent": None, "guard": None, "translation": None, "usage": []}
    scene = req.scene or req.npc.scene

    async def intent():
        out = await llm.chat(
            "intent", "intent", P.intent_messages(req.npc, req.state, req.history, text), usage, 0.0, 200, P.intent_schema(req.state)
        )
        return clean_intent(json.loads(out), req.state)

    # 로컬 판정기를 켰을 때만 guard에 붙는 칸: 누가 판정했나·로컬 점수·로컬 실패 (LLM이 실패해도 남도록 밖에 둔다)
    trace = {"source": "llm", "score": None, "local_error": None} if local else {}

    async def guard():
        if local and HANGUL.search(text):
            prev = next((h.text for h in reversed(req.history) if h.who == "npc"), "")
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
        out = await llm.chat("guard", "guard", P.guard_messages(req.npc, req.history, text), usage, 0.0, 5)
        return guard_result(out)

    async def translate():
        out = await llm.chat("translate", "translate", P.translate_messages(req.npc, scene, req.history, text), usage, 0.0, 80)
        return out.split("\n")[0].strip().strip('"')

    jobs = [intent(), guard()] + ([translate()] if P.HANGUL.search(text) else [])
    res = await asyncio.gather(*(within(j, llm.s.judge_budget_s) for j in jobs), return_exceptions=True)
    it = res[0] if not isinstance(res[0], BaseException) else {"id": "unclear", "ids": ["unclear"], "slots": {}, "error": str(res[0])[:160]}
    gd = (res[1] if not isinstance(res[1], BaseException) else guard_failure(res[1])) | trace
    translation = None
    if len(res) == 3:
        english = "" if isinstance(res[2], BaseException) else res[2]
        english = "" if english == "-" else english
        # 무례한 말은 번역 카드를 보여 주지 않는다 (욕을 영어로 가르치는 꼴). 판정 못 한 말도 보여 주지 않음
        translation = {
            "english": english or None,
            "show": bool(english) and gd["abusive"] is False,
            "error": str(res[2])[:160] if isinstance(res[2], BaseException) else None,
        }
    for r in res:  # 프로그램 오류(버그)는 숨기지 않는다
        if isinstance(r, BaseException) and not isinstance(r, (ProviderError, ValueError)):
            raise r
    return {
        "text": text,
        "dropped": False,
        "notice": P.ACTION_PART_KO if had_action else None,
        "intent": it,
        "guard": gd,
        "translation": translation,
        "usage": usage.items,
    }


async def hint(llm: LLM, req: HintRequest) -> dict:
    usage = Usage()
    out = await llm.chat(
        "hint", "hint", P.hint_messages(req.npc, req.scene or req.npc.scene, req.goal, req.history), usage, 0.3, 300, P.HINT_SCHEMA
    )
    try:
        res = json.loads(out)
    except ValueError as e:
        raise ModelOutputError("힌트 출력이 JSON이 아님") from e
    say_this = res.get("say_this") if isinstance(res, dict) else None
    keywords = res.get("keywords") if isinstance(res, dict) else None
    if not (isinstance(say_this, list) and isinstance(keywords, list)):
        raise ModelOutputError("힌트 출력에 say_this·keywords 목록이 없음")
    return {"say_this": say_this[:2], "keywords": keywords[:3], "usage": usage.items}


async def end_conversation(llm: LLM, req: EndRequest) -> dict:
    """피드백과 기억 카드를 동시에. 하나가 실패하면 그 칸만 null + errors (Spring은 이전 카드를 그대로 둠)."""
    usage = Usage()

    async def feedback():
        out = await llm.chat(
            "feedback",
            "feedback",
            P.feedback_messages(req.npc, req.scenario_title, req.goal_reached, req.outcome, req.history),
            usage,
            0.2,
            900,
            P.FEEDBACK_SCHEMA,
        )
        fb = json.loads(out)
        if not isinstance(fb, dict):
            raise ModelOutputError("피드백 출력이 JSON 객체가 아님")
        return fb

    async def memory():
        out = await llm.chat(
            "memory", "memory", P.memory_messages(req.npc, req.memory_card, req.outcome, req.history), usage, 0.0, 500, P.MEMORY_SCHEMA
        )
        return P.clean_card(json.loads(out))

    fb, mem = await asyncio.gather(feedback(), memory(), return_exceptions=True)
    for r in (fb, mem):
        if isinstance(r, BaseException) and not isinstance(r, (ProviderError, ValueError)):
            raise r
    err = lambda r: str(r)[:160] if isinstance(r, BaseException) else None
    return {
        "feedback": None if err(fb) else fb,
        "memory_card": None if err(mem) else mem,
        "errors": {"feedback": err(fb), "memory": err(mem)},
        "usage": usage.items,
    }


# ---------- 음성 인식 ----------
# Whisper 프롬프트: 한영 혼합 예시를 주면 "muffin 하나"의 muffin을 한글로 바꾸지 않는다 (asr_eval 음성으로 확인)
STT_PROMPT = "I want... 아이스 라떼... Can I get a muffin, 음, 하나? The bus stop is over there."
STT_MAX_BYTES = 3 * 1024 * 1024
AUDIO_EXT = {"audio/webm": "webm", "audio/ogg": "ogg", "audio/mp4": "mp4", "audio/wav": "wav", "audio/x-wav": "wav", "audio/mpeg": "mp3"}
NORM = re.compile(r"[^a-z0-9가-힣 ]")


def stt_prompt(scene_words: list[str]) -> str:
    """장면 단어 보정: 지금 장면의 단어(메뉴·상품·거리 이름)를 프롬프트에 붙여 철자를 맞춘다."""
    words = [w.strip() for w in scene_words if w.strip()][:30]
    return STT_PROMPT + (" " + ", ".join(words) + "." if words else "")


def is_prompt_echo(text: str, prompt: str) -> bool:
    """Whisper는 무음에 프롬프트를 되풀이한다. 프롬프트의 긴 조각과 같으면 버린다.
    (짧은 말 'Latte.'는 프롬프트 단어와 같아도 진짜 발화일 수 있어 12자 미만은 거르지 않는다)"""
    t = NORM.sub("", text.lower()).strip()
    return len(t) >= 12 and t in NORM.sub("", prompt.lower())


async def stt(llm: LLM, audio: bytes, mime: str, scene_words: list[str]) -> dict:
    usage = Usage()
    mime = (mime or "audio/webm").split(";")[0].strip()
    prompt = stt_prompt(scene_words)
    out = await llm.transcribe(audio, f"speech.{AUDIO_EXT.get(mime, 'webm')}", mime, prompt, usage)
    text = "" if is_prompt_echo(out["text"], prompt) else out["text"]
    return {"text": text, "latency_s": out["latency_s"], "audio_s": out["audio_s"], "usage": usage.items}
