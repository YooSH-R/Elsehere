"""판정·힌트·뜻 보기·추천 단어·기억 카드. 상태는 바꾸지 않는다 (상태 전환은 Spring 규칙)."""

import asyncio
import json
import logging

from . import npc_relationship
from . import prompts as P
from .llm import LLM, ModelOutputError, ProviderError, Usage, within
from .schemas import (
    FollowUpRequest,
    HintRequest,
    JudgeRequest,
    MeaningRequest,
    MemoryRequest,
    RelationshipRequest,
    State,
    SuggestRequest,
)

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


def clean_events(raw: dict) -> list[str]:
    """판정 출력의 줄 사건. 목록(P.LINE_EVENTS) 밖 값·중복·무례는 버리고, 없거나 목록이 아니면 []."""
    got = raw.get("events") if isinstance(raw.get("events"), list) else []
    return [event for event in dict.fromkeys(e for e in got if isinstance(e, str)) if event in P.LINE_EVENTS]


async def judge(llm: LLM, req: JudgeRequest) -> dict:
    """의도(+ 줄 사건)와 번역(한국어가 섞였을 때)을 동시에. 하나가 실패해도 턴은 이어 간다 (의도 → unclear·사건 없음, 번역 → error).
    무례 판정은 /v1/guard가 따로 한다. 번역 카드를 보여 줄지(무례가 아님이 확정일 때만)는 그 결과로 Spring이 정한다.
    affinity_delta는 줄 사건으로 바로 올릴 호감이다. 무례로 확정된 턴이면 Spring이 쓰지 않고 /v1/guard의 감점만 쓴다."""
    usage = Usage()
    text, had_action = P.strip_actions(req.text)
    if not text:  # 행동만 있었으면 모델을 부르지 않는다. Spring은 턴으로 세지 않음
        return {
            "text": "",
            "dropped": True,
            "notice": P.ACTION_ONLY_KO,
            "intent": None,
            "translation": None,
            "events": None,
            "affinity_delta": None,
            "usage": [],
        }
    scene = req.scene or req.npc.scene

    async def intent():
        out = await llm.chat(
            "intent", "intent", P.intent_messages(req.npc, req.state, req.history, text), usage, 0.0, 200, P.intent_schema(req.state)
        )
        raw = json.loads(out)
        return clean_intent(raw, req.state), clean_events(raw)

    async def translate():
        out = await llm.chat("translate", "translate", P.translate_messages(req.npc, scene, req.history, text), usage, 0.0, 80)
        return out.split("\n")[0].strip().strip('"')

    budget = llm.s.judge_budget_s
    jobs = [within(intent(), budget)]
    if P.HANGUL.search(text):
        jobs.append(within(translate(), budget))
    res = await asyncio.gather(*jobs, return_exceptions=True)
    failed = {"id": "unclear", "ids": ["unclear"], "slots": {}, "error": str(res[0])[:160]}
    it, events = res[0] if not isinstance(res[0], BaseException) else (failed, [])
    translation = None
    if len(res) == 2:
        english = "" if isinstance(res[1], BaseException) else res[1]
        english = "" if english == "-" else english
        translation = {"english": english or None, "error": str(res[1])[:160] if isinstance(res[1], BaseException) else None}
    for r in res:  # 프로그램 오류(버그)는 숨기지 않는다
        if isinstance(r, BaseException) and not isinstance(r, (ProviderError, ValueError)):
            raise r
    return {
        "text": text,
        "dropped": False,
        "notice": P.ACTION_PART_KO if had_action else None,
        "intent": it,
        "translation": translation,
        "events": events,
        "affinity_delta": line_affinity(req, events),
        "usage": usage.items,
    }


def line_affinity(req: JudgeRequest, events: list[str]) -> int:
    """이 줄의 사건으로 바로 올릴 호감. 이력의 학습자 줄에서 이번 대화에 이미 센 사건은 다시 세지 않는다(무례 줄의 사건은 센 적 없음)."""
    rules, library = npc_relationship.service_rules()
    rates = npc_relationship.npc_rates(req.npc.profile, rules, library)
    earlier = [event for line in req.history if line.who == "me" for event in npc_relationship.turn_events((line.rude, line.events))]
    return npc_relationship.line_affinity_delta(earlier, events, rates, rules)


async def hint(llm: LLM, req: HintRequest) -> dict:
    usage = Usage()
    out = await llm.chat(
        "hint",
        "hint",
        P.hint_messages(req.npc, req.scene or req.npc.scene, req.goal, req.history, req.level),
        usage,
        0.3,
        300,
        P.HINT_SCHEMA,
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


def load_object(out: str, what: str) -> dict:
    """모델 출력(JSON 문자열)을 객체로. 아니면 ModelOutputError."""
    try:
        res = json.loads(out)
    except ValueError as e:
        raise ModelOutputError(f"{what} 출력이 JSON이 아님") from e
    if not isinstance(res, dict):
        raise ModelOutputError(f"{what} 출력이 JSON 객체가 아님")
    return res


async def meaning(llm: LLM, req: MeaningRequest) -> dict:
    """NPC 대사의 한국어 뜻과 단어 풀이. 풀이는 대사에 실제로 있는 말만 (화면이 그 말에 뜻을 붙인다)."""
    usage = Usage()
    out = await llm.chat("meaning", "translate", P.meaning_messages(req.npc, req.text, req.level), usage, 0.0, 300, P.MEANING_SCHEMA)
    res = load_object(out, "뜻 보기")
    meaning_ko, items = res.get("meaning_ko"), res.get("glossary")
    if not (isinstance(meaning_ko, str) and meaning_ko.strip() and isinstance(items, list)):
        raise ModelOutputError("뜻 보기 출력에 meaning_ko·glossary가 없음")
    line, glossary = req.text.casefold(), []
    for it in items:
        if not isinstance(it, dict):
            continue
        text, ko = str(it.get("text") or "").strip(), str(it.get("meaning_ko") or "").strip()
        if text and ko and text.casefold() in line and text.casefold() not in {g["text"].casefold() for g in glossary}:
            glossary.append({"text": text, "meaning_ko": ko})
    return {"meaning_ko": meaning_ko.strip(), "glossary": glossary[:3], "usage": usage.items}


async def suggest(llm: LLM, req: SuggestRequest) -> dict:
    """학습자가 다음 대답에 쓸 단어·짧은 구 3개 (공백 정리, 대소문자 무시 중복 제거)."""
    usage = Usage()
    msgs = P.suggest_messages(req.npc, req.scene or req.npc.scene, req.goal, req.history, req.level)
    res = load_object(await llm.chat("suggest", "hint", msgs, usage, 0.3, 80, P.SUGGEST_SCHEMA), "추천 단어")
    words = []
    for w in res.get("words") if isinstance(res.get("words"), list) else []:
        w = w.strip() if isinstance(w, str) else ""
        if w and len(w) <= 40 and w.casefold() not in {x.casefold() for x in words}:
            words.append(w)
    if not words:
        raise ModelOutputError("추천 단어 출력에 단어가 없음")
    return {"words": words[:3], "usage": usage.items}


async def memory(llm: LLM, req: MemoryRequest) -> dict:
    """NPC 기억 카드 갱신. 실패하면 HTTP 오류이고 Spring은 이전 카드를 그대로 둔다."""
    usage = Usage()
    out = await llm.chat(
        "memory",
        "memory",
        P.memory_messages(req.npc, req.memory_card, req.outcome, req.history),
        usage,
        0.0,
        500,
        P.MEMORY_SCHEMA,
        hedge=False,
    )
    return {"memory_card": P.parse_memory_card(out), "usage": usage.items}


async def follow_up(llm: LLM, req: FollowUpRequest) -> dict:
    """대화가 끝난 뒤 NPC가 지금 준비해 곧 건네기로 한 것. 없으면 follow_up이 null이다. 실패하면 HTTP 오류이고 Spring은 다시 부르지 않는다."""
    usage = Usage()
    out = await llm.chat(
        "follow_up",
        "memory",
        P.follow_up_messages(req.npc, req.history, req.kind),
        usage,
        0.0,
        120,
        P.FOLLOW_UP_KINDS[req.kind][1],
        hedge=False,
    )
    return {"follow_up": P.parse_follow_up(load_object(out, "건넬 것")), "usage": usage.items}


def relationship(req: RelationshipRequest) -> dict:
    """정상 종료된 대화 하나로 바뀌는 알아봄의 변화량. 모델을 부르지 않는다.
    호감은 대화 중에 발화마다 이미 바꿨으므로(사건은 /v1/judge, 무례는 /v1/guard의 affinity_delta) 여기서는 계산하지 않는다."""
    rules, library = npc_relationship.service_rules()
    rates = npc_relationship.npc_rates(req.npc.profile, rules, library)
    before = req.relationship.model_dump() if req.relationship else None
    return npc_relationship.after_conversation(before, rates, rules)
