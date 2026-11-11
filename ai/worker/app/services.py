"""판정·힌트·뜻 보기·추천 단어·레벨 테스트 말하기 판정·기억 카드. 상태는 바꾸지 않는다 (상태 전환은 Spring 규칙)."""

import asyncio
import json
import logging
import re

from . import npc_relationship
from . import prompts as P
from .llm import LLM, ModelOutputError, ProviderError, Usage, within
from .schemas import (
    FollowUpRequest,
    HintRequest,
    JudgeRequest,
    LevelTestSpeakingRequest,
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


def clean_english_correction(raw: dict, text: str) -> str | None:
    """영어 교정문만 표현 카드로 보낸다. 번역·표면 표기 차이는 여기서 만들지 않는다."""
    correction = raw.get("english_correction")
    if P.HANGUL.search(text) or not isinstance(correction, str):
        return None
    correction = correction.strip()
    if not re.search(r"[A-Za-z]", correction) or P.HANGUL.search(correction):
        return None

    def words(value: str) -> list[str]:
        return re.findall(r"\w+(?:'\w+)*", value.casefold().replace("’", "'"))

    return correction if words(correction) != words(text) else None


async def judge(llm: LLM, req: JudgeRequest) -> dict:
    """의도·줄 사건·영어 교정을 함께 판정하고, 한국어 번역은 동시에 받는다. 실패해도 턴은 이어 간다.
    무례 판정은 /v1/guard가 따로 한다. 생성된 영어 번역은 무례 판정과 관계없이 Spring이 표현 카드로 전달한다.
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
    hedge_after_s = llm.s.judge_hedge_after_s  # NPC 대답이 판정을 기다리므로 다른 호출보다 일찍 같은 요청을 더 보낸다

    async def intent():
        out = await llm.intent_chat(
            P.intent_messages(req.npc, req.state, req.history, text), usage, P.intent_schema(req.state), hedge_after_s=hedge_after_s
        )
        raw = json.loads(out)
        return clean_intent(raw, req.state), clean_events(raw), clean_english_correction(raw, text)

    async def translate():
        out = await llm.chat(
            "translate", "translate", P.translate_messages(req.npc, scene, req.history, text), usage, 0.0, 80, hedge_after_s=hedge_after_s
        )
        return unwrap_quotes(out.split("\n")[0].strip())

    budget = llm.s.judge_budget_s
    jobs = [within(intent(), budget)]
    if P.HANGUL.search(text):
        jobs.append(within(translate(), budget))
    res = await asyncio.gather(*jobs, return_exceptions=True)
    failed = {"id": "unclear", "ids": ["unclear"], "slots": {}, "error": str(res[0])[:160]}
    it, events, correction = res[0] if not isinstance(res[0], BaseException) else (failed, [], None)
    translation = {"english": correction, "error": None} if correction else None
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


def unwrap_quotes(line: str) -> str:
    """모델이 번역문 전체를 따옴표로 감싸면 벗긴다. 이름을 그대로 둔 따옴표('My name is "김사피"')는 남긴다."""
    inner = line[1:-1]
    if len(line) >= 2 and line[0] == line[-1] == '"' and inner.count('"') % 2 == 0:
        return inner
    return line


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
    # 풀이가 단어마다 하나씩 25개까지 늘어 출력이 길어진다
    out = await llm.chat("meaning", "translate", P.meaning_messages(req.npc, req.text, req.level), usage, 0.0, 1200, P.MEANING_SCHEMA)
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
    # 이름은 굽은 따옴표로 받는다(JSON 안의 곧은 따옴표는 모델이 이스케이프하지 않으면 문자열이 닫혀 뜻이 이름 앞에서 잘린다).
    # 화면은 곧은 따옴표로 보여 준다
    meaning_ko = meaning_ko.strip().replace("“", '"').replace("”", '"')
    return {"meaning_ko": meaning_ko, "glossary": glossary[:25], "usage": usage.items}


async def suggest(llm: LLM, req: SuggestRequest) -> dict:
    """다음 답변을 만드는 어휘와 뜻. 기존 words 문자열 배열 계약을 유지한다."""
    usage = Usage()
    msgs = P.suggest_messages(req.npc, req.scene or req.npc.scene, req.goal, req.history, req.level)
    res = load_object(await llm.chat("suggest", "hint", msgs, usage, 0.3, 320, P.SUGGEST_SCHEMA), "추천 단어")
    words = []
    seen = set()
    for w in res.get("words") if isinstance(res.get("words"), list) else []:
        if isinstance(w, dict):
            text, meaning = w.get("text"), w.get("meaning_ko")
            if w.get("kind") != "vocabulary" or not isinstance(text, str) or not isinstance(meaning, str):
                continue
            text, meaning = " ".join(text.split()), " ".join(meaning.split())
            if not meaning or len(meaning) > 60:
                continue
            label = f"{text} — {meaning}"
        else:  # 이전 고정 응답·캐시와의 호환. 신규 모델 출력은 위의 구조를 사용한다.
            text = w.strip() if isinstance(w, str) else ""
            label = text
        if text and len(text) <= 60 and text.casefold() not in seen:
            seen.add(text.casefold())
            words.append(label)
    if not words:
        raise ModelOutputError("추천 단어 출력에 단어가 없음")
    return {"words": words[:3], "usage": usage.items}


async def level_test_speaking(llm: LLM, req: LevelTestSpeakingRequest) -> dict:
    """레벨 테스트 말하기 문항마다 통과 여부와 한국어 한 줄 피드백. 학습자 영어를 평가하는 일이라 피드백 역할의 모델을 쓴다.
    결과는 요청 문항 순서다. 모델이 문항을 빠뜨리거나, 요청에 없는 문항을 내거나, passed가 불이 아니거나, 피드백이 비면 ModelOutputError."""
    usage = Usage()
    msgs = P.level_test_speaking_messages(req.items)
    out = await llm.chat("level_test_speaking", "feedback", msgs, usage, 0.0, 400, P.LEVEL_TEST_SPEAKING_SCHEMA)
    res = load_object(out, "레벨 테스트 말하기 판정")
    results, feedback_ko = res.get("results"), res.get("feedback_ko")
    if not (isinstance(results, list) and isinstance(feedback_ko, str) and feedback_ko.strip()):
        raise ModelOutputError("레벨 테스트 말하기 판정 출력에 results·feedback_ko가 없음")
    item_codes = [item.item_code for item in req.items]
    passed_by_code: dict[str, bool] = {}
    for result in results:
        if not isinstance(result, dict):
            raise ModelOutputError("레벨 테스트 말하기 판정 결과가 JSON 객체가 아님")
        code, passed = result.get("item_code"), result.get("passed")
        if code not in item_codes or code in passed_by_code:
            raise ModelOutputError(f"레벨 테스트 말하기 판정 출력에 요청에 없거나 겹친 문항이 있음: {code}")
        if not isinstance(passed, bool):
            raise ModelOutputError(f"레벨 테스트 말하기 판정 출력의 passed가 참·거짓이 아님: {code}")
        passed_by_code[code] = passed
    missing = [code for code in item_codes if code not in passed_by_code]
    if missing:
        raise ModelOutputError(f"레벨 테스트 말하기 판정 출력에 빠진 문항이 있음: {', '.join(missing)}")
    return {
        "results": [{"item_code": code, "passed": passed_by_code[code]} for code in item_codes],
        "feedback_ko": feedback_ko.strip(),
        "usage": usage.items,
    }


async def memory(llm: LLM, req: MemoryRequest) -> dict:
    """NPC 기억 카드 갱신과 이번 대화의 인상. 실패하면 HTTP 오류이고 Spring은 이전 카드·호감을 그대로 둔다.
    affinity_delta는 좋은 대화로 올릴 호감이다(app.npc_relationship.good_conversation_affinity_delta). Spring이 카드를 저장하면서 더한다."""
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
    card, impression = P.parse_memory_output(out)
    rules, _ = npc_relationship.service_rules()
    learner_turns = [(line.rude, line.events) for line in req.history if line.who == "me"]
    return {
        "memory_card": card,
        "impression": impression,
        "affinity_delta": npc_relationship.good_conversation_affinity_delta(impression, learner_turns, rules),
        "usage": usage.items,
    }


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
