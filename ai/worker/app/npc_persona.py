"""NPC 인격 카드 만들기: MBTI → 성격 5축, 행동 조각 뽑기, 자동 칸, 재료로 고르는 규칙 뼈대, LLM이 쓰는 사연.
설계는 docs/npc-life-design.md 10절. 조합 규칙은 여기 한 곳에만 두고, 저장소 도구(ai/tools)와 워커 API가 함께 쓴다.

MBTI는 입력하기 편한 앞문이고, 계산과 인격 스키마(personality.dimensions)는 Big Five 5축(0~1)을 쓴다.
MBTI를 성격의 정답으로 다루지 않고 캐릭터 재료로만 쓴다. 규칙 데이터는 ai/npcs/rules(행동 조각, 생성 재료).
"""

import json
import random
import re
from functools import cache
from pathlib import Path

from pydantic import ValidationError

from .llm import LLM, ModelOutputError, Usage
from .npc_profile_models import NpcConfiguration

RULES = Path(__file__).resolve().parents[2] / "npcs/rules"  # 저장소의 ai/npcs/rules. 배포 이미지에도 같은 자리에 복사한다


def load_behaviors() -> dict:
    return json.loads((RULES / "behaviors.json").read_text(encoding="utf-8"))


def load_ingredients() -> dict:
    return json.loads((RULES / "persona-ingredients.json").read_text(encoding="utf-8"))


# ---------- MBTI → 성격 5축 ----------
MBTI = re.compile(r"^([EI])([NS])([FT])([JP])(?:-([AT]))?$")
LEANING = 0.75  # MBTI는 둘 중 하나로만 나눠 강도를 모르므로, 끝값(0·1) 대신 한쪽으로 기운 값
NEUROTICISM = {"T": 0.7, "A": 0.3, None: 0.5}  # 16Personalities식 꼬리 -T(예민)·-A(안정), 없으면 중간
AXES = ("openness", "conscientiousness", "extraversion", "agreeableness", "neuroticism")  # 인격 스키마 personality.dimensions 칸 순서


def mbti_to_dimensions(mbti: str) -> dict[str, float]:
    """MBTI(예: ESFJ-T)를 성격 5축으로. E/I → 외향성, N/S → 개방성, F/T → 친화성, J/P → 성실성, -T/-A → 신경성."""
    match = MBTI.match(mbti.upper())
    if not match:
        raise ValueError(f"MBTI 형식이 아님: {mbti!r} (예: ESFJ, ESFJ-T)")
    energy, perceiving, deciding, living, identity = match.groups()
    return {
        "extraversion": _lean(energy == "E"),
        "openness": _lean(perceiving == "N"),
        "agreeableness": _lean(deciding == "F"),
        "conscientiousness": _lean(living == "J"),
        "neuroticism": NEUROTICISM[identity],
    }


def _lean(high: bool) -> float:
    return LEANING if high else round(1 - LEANING, 2)


def side(value: float) -> str:
    """성격 축 값이 어느 쪽인지: 0.5보다 크면 high, 작으면 low, 같으면 middle."""
    return "high" if value > 0.5 else "low" if value < 0.5 else "middle"


SPEECH_AXES = ("extraversion", "openness", "agreeableness", "conscientiousness")  # MBTI 순서 E/I·N/S·F/T·J/P


@cache
def load_temperament_speech() -> dict:
    """성격 말투 규칙(ai/npcs/rules/temperament-speech.json). 대답마다 읽지 않도록 한 번만 읽는다(바꾸지 말고 읽기만)."""
    return json.loads((RULES / "temperament-speech.json").read_text(encoding="utf-8"))


def temperament_lines(dimensions: dict[str, float]) -> list[str]:
    """성격 5축에서 MBTI 네 축의 말투·행동 규칙 문장을 고른다. 축이 기운 쪽(side)의 문장이고, 딱 가운데(0.5)인 축은 뺀다.
    MBTI에서 만든 카드는 축이 0.25·0.75라 네 문장이 모두 들어가고, MBTI 없이 5축만 적은 NPC도 같은 규칙을 쓴다."""
    axes = load_temperament_speech()["axes"]
    return [axes[axis][side(dimensions[axis])] for axis in SPEECH_AXES if side(dimensions[axis]) != "middle"]


def lean_fit(leans: dict[str, str], dimensions: dict[str, float]) -> float:
    """성격 쪽 표시(축: high/low)가 이 성격에 맞는 정도 0~1. 기운 쪽의 축 값을 평균한다. 0.5 이하면 그 성격 쪽이 아니다."""
    sides = [dimensions[axis] if side == "high" else 1 - dimensions[axis] for axis, side in leans.items()]
    return sum(sides) / len(sides)


# ---------- 행동 조각과 자동 칸 ----------
BEHAVIORS_PER_NPC = 4


def pick_behaviors(dimensions: dict[str, float], library: dict, seed: str, count: int = BEHAVIORS_PER_NPC) -> list[dict]:
    """행동 조각 모음에서 이 성격 쪽에 맞는 조각을 뽑는다. 잘 맞을수록 뽑힐 확률이 높고(가중 추첨),
    범주는 겹치지 않으며, 서로 충돌하는 조각은 함께 뽑지 않는다. seed(보통 npcId)가 같으면 언제나 같은 결과다."""
    rng = random.Random(seed)
    candidates = [(behavior, fit) for behavior in library["behaviors"] if (fit := lean_fit(behavior["leans"], dimensions)) > 0.5]
    picked: list[dict] = []
    while len(picked) < count:
        eligible = [(behavior, fit) for behavior, fit in candidates if _can_join(behavior, picked)]
        if not eligible:
            break
        picked.append(rng.choices([behavior for behavior, _ in eligible], weights=[fit for _, fit in eligible])[0])
    return picked


def _can_join(behavior: dict, picked: list[dict]) -> bool:
    """이미 뽑힌 조각과 범주가 겹치지 않고, 어느 쪽에서 적었든 충돌 표시가 없어야 함께 뽑힌다."""
    picked_ids = {other["id"] for other in picked}
    blocked_by_picked = {conflict for other in picked for conflict in other.get("conflictsWith", [])}
    return (
        behavior["id"] not in picked_ids
        and behavior["category"] not in {other["category"] for other in picked}
        and not set(behavior.get("conflictsWith", [])) & picked_ids
        and behavior["id"] not in blocked_by_picked
    )


def with_derived_traits(profile: dict, life: dict, library: dict) -> dict:
    """인격 카드(npcProfile)에 자동 칸을 더한 새 카드. 손으로 쓴 글은 그대로 둔다.
    - personality.dimensions: 생활 파일 MBTI의 성격 5축
    - behaviorRules: 손으로 쓴 규칙 뒤에 뽑힌 행동 조각의 문장. 예전에 더한 조각 문장은 빼고 다시 더하므로 몇 번 계산해도 같다"""
    dimensions = mbti_to_dimensions(life["mbti"])
    picked = pick_behaviors(dimensions, library, seed=life["npcId"])
    fragment_texts = {behavior["text"] for behavior in library["behaviors"]}
    handwritten = [rule for rule in profile.get("behaviorRules", []) if rule not in fragment_texts]
    return {
        **profile,
        "personality": {**profile["personality"], "dimensions": {axis: dimensions[axis] for axis in AXES}},
        "behaviorRules": handwritten + [behavior["text"] for behavior in picked],
    }


# ---------- 규칙 뼈대 ----------
VALUES_PER_NPC, MOTIVATIONS_PER_NPC, INTERESTS_PER_NPC = 2, 2, 3
MBTI_TYPES = [e + n + f + j for e in "EI" for n in "NS" for f in "FT" for j in "JP"]
# 백엔드가 DB 후보로 바꿀 수 있는 목록. 말투·사회적 행동은 성격 축으로 고르는 규칙이라 늘 AI 기본 재료를 쓴다
CANDIDATE_LISTS = ("names", "ageRanges", "occupations", "traits", "values", "motivations", "interests")


def with_candidates(defaults: dict, candidates: dict) -> dict:
    """백엔드가 보낸 후보 목록으로 기본 재료를 바꾼다. 보낸 목록만 바꾸고, 보내지 않았거나 빈 목록은 기본 재료를 쓴다."""
    return {**defaults, **{key: value for key, value in candidates.items() if key in CANDIDATE_LISTS and value}}


def generate(
    npc_id: str,
    role: str,
    place: str,
    ingredients: dict,
    library: dict,
    mbti: str | None = None,
    fixed: dict | None = None,
    taken_names=frozenset(),
) -> tuple[dict, dict]:
    """입력과 재료로 인격 카드 뼈대를 만든다. LLM 없이 규칙으로만 고르고 npcId가 씨앗이라, 재료가 같으면 언제나 같은 NPC다.
    mbti가 없으면 npcId로 고른다. fixed의 칸(name·occupation·ageRange·values·motivations·interests)은 그대로 쓰고
    나머지만 고른다. 칸마다 씨앗을 따로 써서 한 칸을 고정해도 다른 칸은 바뀌지 않는다.
    (인격 설정, 생활 파일의 성격·역할 입력 노드). 근무지·교대는 정하지 않는다(생성 명령이 붙인다).
    taken_names는 다른 NPC가 이미 쓰는 이름이라 고르지 않는다."""
    fixed = fixed or {}

    def rng(field: str) -> random.Random:
        return random.Random(f"{npc_id}:{field}")

    mbti = mbti or rng("mbti").choice(MBTI_TYPES) + rng("identity").choice(["-A", "-T"])
    dimensions = mbti_to_dimensions(mbti)
    job = _occupation(ingredients, role, place, fixed.get("occupation"), rng("occupation"))
    profile = {
        "schemaVersion": "2.0",
        "profileVersion": 1,
        "name": fixed.get("name") or _name(ingredients["names"], taken_names, rng("name")),
        "occupation": job["text"],
        "ageRange": fixed.get("ageRange") or rng("ageRange").choice(job.get("ages", ingredients["ageRanges"])),
        "personality": {"traits": _traits(ingredients["traits"], dimensions, rng("traits"))},
        "values": fixed.get("values") or _texts(ingredients["values"], dimensions, role, rng("values"), VALUES_PER_NPC),
        "motivations": fixed.get("motivations")
        or _texts(ingredients["motivations"], dimensions, role, rng("motivations"), MOTIVATIONS_PER_NPC),
        "socialBehavior": _social_behavior(ingredients["socialBehavior"], dimensions, role),
        "speechStyle": _speech_style(ingredients["speechStyle"], dimensions, role),
        "background": {
            "summary": None,
            "experiences": [],
            "interests": fixed.get("interests") or _texts(ingredients["interests"], dimensions, role, rng("interests"), INTERESTS_PER_NPC),
        },
        "behaviorRules": [],
    }
    life = {"schemaVersion": "1.0", "npcId": npc_id, "mbti": mbti, "role": role}
    profile = with_derived_traits(profile, life, library)
    config = {"npcId": npc_id, "npcProfile": profile, "defaultLocationId": place, "voiceId": None, "isActive": False}  # 목소리 미연결 초안
    return config, life


def _name(names: list[str], taken_names, rng: random.Random) -> str:
    free = [name for name in names if name not in taken_names]
    if not free:
        raise ValueError("고를 이름이 없음: 이름 후보를 모두 다른 NPC가 쓰고 있음")
    return rng.choice(free)


def _occupation(ingredients: dict, role: str, place: str, fixed: str | None, rng: random.Random) -> dict:
    """고정한 직업이 있으면 그 글(후보에 있으면 그 나이대도), 없으면 역할에 맞는 직업 하나. 직원은 일하는 장소까지 맞아야 한다."""
    if fixed:
        return next((job for job in ingredients["occupations"] if job["text"] == fixed), {"text": fixed})
    fitting = [job for job in ingredients["occupations"] if job["role"] == role and job.get("place", place) == place]
    if not fitting:
        raise ValueError(f"{role} 역할로 {place}에서 할 직업이 재료에 없음")
    return rng.choice(fitting)


def _traits(traits: list[dict], dimensions: dict[str, float], rng: random.Random) -> list[dict]:
    """성격 축마다 NPC가 기운 쪽의 특징 하나. 가운데(0.5)인 축은 넣지 않는다."""
    picked = []
    for axis in AXES:
        leaning = side(dimensions[axis])
        if leaning != "middle":
            options = [trait for trait in traits if trait["leans"] == {axis: leaning}]
            if not options:
                raise ValueError(f"성격 특징 후보에 {axis} {leaning} 쪽이 없음")
            trait = rng.choice(options)
            picked.append({"name": trait["name"], "description": trait["description"]})
    return picked


def _texts(items: list[dict], dimensions: dict[str, float], role: str, rng: random.Random, count: int) -> list[str]:
    """이 역할이 쓸 수 있고 성격 쪽이 맞거나 표시가 없는 재료에서 count개. 잘 맞을수록 잘 뽑히고(표시 없음은 0.5) 같은 것은 한 번만."""
    pool = []
    for item in items:
        fit = lean_fit(item["leans"], dimensions) if "leans" in item else 0.5
        if role in item.get("roles", [role]) and (fit > 0.5 or "leans" not in item):
            pool.append((item["text"], fit))
    picked = []
    while pool and len(picked) < count:
        text = rng.choices([text for text, _ in pool], weights=[fit for _, fit in pool])[0]
        picked.append(text)
        pool = [(other, fit) for other, fit in pool if other != text]
    return picked


def _by_side(rule: dict, dimensions: dict[str, float]) -> str:
    return rule[side(dimensions[rule["axis"]])]


def _social_behavior(rules: dict, dimensions: dict[str, float], role: str) -> dict:
    return {
        "firstMeeting": _by_side(rules["firstMeeting"], dimensions),
        "familiarityExpression": _by_side(rules["familiarityExpression"], dimensions),
        "conflictResponse": _by_side(rules["conflictResponse"], dimensions),
        "boundaries": list(rules["boundaries"][role]),
    }


def _speech_style(rules: dict, dimensions: dict[str, float], role: str) -> dict:
    return {
        "tone": " and ".join(_by_side(rule, dimensions) for rule in rules["tone"]),
        "formality": rules["formality"][role],
        "verbosity": _by_side(rules["verbosity"], dimensions),
        "directness": _by_side(rules["directness"], dimensions),
    }


# ---------- 사연 (LLM) ----------
NARRATE_SYSTEM = """You write the backstory for a character in an English conversation game set in a small American town.
You get the character's card as JSON. Write only:
- "summary": two or three plain sentences about who they are and how they came to be this way.
- "experiences": one or two past events, each with "event" (what happened, one sentence) and "influence" (how it shapes the way they act now, one sentence).
Tie the story to the traits, values, motivations, interests and behavior rules on the card so they read as one person, and give a reason behind at least one of them.
Never change or contradict the card. Do not name real people or brands, and do not mention any places, shops, streets or services in the town: the game decides those.
Keep it ordinary and warm-hearted: no tragedy, crime, illness or stereotypes."""

NARRATE_SCHEMA = {
    "type": "object",
    "properties": {
        "summary": {"type": "string"},
        "experiences": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"event": {"type": "string"}, "influence": {"type": "string"}},
                "required": ["event", "influence"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["summary", "experiences"],
    "additionalProperties": False,
}


async def narrate(config: dict, llm: LLM, usage: Usage | None = None) -> dict:
    """규칙 뼈대의 사연 칸(background.summary·experiences)만 LLM이 쓴 새 설정. 다른 칸은 그대로다.
    결과가 공통 인격 모델을 지키지 않으면 ModelOutputError (저장하지 않는다). usage에 모델 호출을 남긴다."""
    profile = config["npcProfile"]
    card = {key: value for key, value in profile.items() if key not in ("schemaVersion", "profileVersion")}
    card["background"] = {"interests": profile["background"]["interests"]}  # 비어 있는 사연 칸은 보여 주지 않는다
    messages = [{"role": "system", "content": NARRATE_SYSTEM}, {"role": "user", "content": json.dumps(card, ensure_ascii=False)}]
    out = await llm.chat("narrate", "npc", messages, usage or Usage(), temperature=0.8, max_tokens=500, schema=NARRATE_SCHEMA, hedge=False)
    try:
        story = json.loads(out)
    except json.JSONDecodeError as e:
        raise ModelOutputError(f"모델 출력이 JSON이 아님: {out[:80]!r}") from e
    background = {**profile["background"], "summary": story["summary"], "experiences": story["experiences"]}
    narrated = {**config, "npcProfile": {**profile, "background": background}}
    try:
        NpcConfiguration.model_validate(narrated)
    except ValidationError as e:
        where = "; ".join(f"{'.'.join(str(part) for part in err['loc'])}: {err['msg']}" for err in e.errors()[:5])
        raise ModelOutputError(f"사연이 공통 인격 모델에 맞지 않음 ({where})") from e
    return narrated
