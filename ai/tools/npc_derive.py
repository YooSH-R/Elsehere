"""NPC 입력 노드(MBTI·성격 5축)에서 공통 연결 규칙으로 생활 값을 계산한다. 설계는 docs/npc-life-design.md 10절.

MBTI는 입력하기 편한 앞문이고, 계산과 인격 스키마(personality.dimensions)는 Big Five 5축(0~1)을 쓴다.
MBTI를 성격의 정답으로 다루지 않고 캐릭터 재료로만 쓴다.
"""

import random
import re

MBTI = re.compile(r"^([EI])([NS])([FT])([JP])(?:-([AT]))?$")
LEANING = 0.75  # MBTI는 둘 중 하나로만 나눠 강도를 모르므로, 끝값(0·1) 대신 한쪽으로 기운 값
NEUROTICISM = {"T": 0.7, "A": 0.3, None: 0.5}  # 16Personalities식 꼬리 -T(예민)·-A(안정), 없으면 중간


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


def derive_rates(dimensions: dict[str, float], rules: dict) -> dict:
    """성격 5축에 공통 연결 규칙(rules["derivation"])을 적용해 생활 값 배수를 계산한다.
    결과는 행동 조각 affects와 같은 모양이다 (예: {"needRates": {"SOCIAL": 1.2}, "recognitionRate": 1.2})."""
    rates: dict = {}
    for rule in rules["derivation"]:
        value = round(rule["at0"] + (rule["at1"] - rule["at0"]) * dimensions[rule["axis"]], 2)
        group, _, key = rule["target"].partition(".")
        if key:
            rates.setdefault(group, {})[key] = value
        else:
            rates[group] = value
    return rates


def npc_life_values(life: dict, rules: dict, library: dict, place: str) -> dict:
    """생활 파일의 입력 노드(mbti·role)와 npcId, 인격 파일의 기본 장소(place)에서 NPC 생활 값을 계산한다.
    - behaviors: 뽑힌 행동 조각 id. npcId를 씨앗으로 써서 같은 NPC는 늘 같은 결과다
    - rates: 성격 계산(derive_rates) × 뽑힌 조각 affects
    - schedule: 역할의 일과 틀에 기본 장소를 넣은 일과"""
    dimensions = mbti_to_dimensions(life["mbti"])
    behaviors = pick_behaviors(dimensions, library, seed=life["npcId"])
    rates = combine_rates(derive_rates(dimensions, rules), *(behavior.get("affects", {}) for behavior in behaviors))
    return {
        "behaviors": [behavior["id"] for behavior in behaviors],
        "rates": rates,
        "schedule": npc_schedule(rules["roles"][life["role"]]["schedule"], place),
    }


def npc_schedule(template: list[dict], place: str) -> list[dict]:
    """역할의 일과 틀에 장소를 넣는다. NPC가 적은 지금은 각자 기본 장소 밖으로 나가지 않고 자리를 비우지 않는다
    (canLeave false → 스스로 채우지 못하는 욕구는 모두 부탁이 된다). 퇴근(OFF)에는 마을에 없으니 장소가 없다."""
    return [dict(block) if block["activity"] == "OFF" else {**block, "scene": place, "canLeave": False} for block in template]


MAX_SALIENCE_BONUS = 2  # 기억 세기는 1~3


def combine_rates(*parts: dict) -> dict:
    """성격에서 계산한 배수와 행동 조각 affects들을 하나로 합친다. 배수는 곱하고, 기억 세기 보너스(salienceBonus)만 더한다."""
    combined: dict = {}
    for part in parts:
        for key, value in part.items():
            if key == "salienceBonus":
                bonuses = combined.setdefault(key, {})
                for event, bonus in value.items():
                    bonuses[event] = min(bonuses.get(event, 0) + bonus, MAX_SALIENCE_BONUS)
            elif isinstance(value, dict):
                scales = combined.setdefault(key, {})
                for name, scale in value.items():
                    scales[name] = round(scales.get(name, 1) * scale, 2)
            else:
                combined[key] = round(combined.get(key, 1) * value, 2)
    return combined


BEHAVIORS_PER_NPC = 4


def pick_behaviors(dimensions: dict[str, float], library: dict, seed: str, count: int = BEHAVIORS_PER_NPC) -> list[dict]:
    """행동 조각 모음에서 이 성격 쪽에 맞는 조각을 뽑는다. 잘 맞을수록 뽑힐 확률이 높고(가중 추첨),
    범주는 겹치지 않으며, 서로 충돌하는 조각은 함께 뽑지 않는다. seed(보통 npcId)가 같으면 언제나 같은 결과다."""
    rng = random.Random(seed)
    candidates = [(behavior, fit) for behavior in library["behaviors"] if (fit := _fit(behavior, dimensions)) > 0.5]
    picked: list[dict] = []
    while len(picked) < count:
        eligible = [(behavior, fit) for behavior, fit in candidates if _can_join(behavior, picked)]
        if not eligible:
            break
        picked.append(rng.choices([behavior for behavior, _ in eligible], weights=[fit for _, fit in eligible])[0])
    return picked


def _fit(behavior: dict, dimensions: dict[str, float]) -> float:
    """조각이 성격에 맞는 정도 0~1. 조각이 기운 쪽(high/low)의 축 값을 평균한다. 0.5 이하면 그 성격 쪽이 아니다."""
    sides = [dimensions[axis] if side == "high" else 1 - dimensions[axis] for axis, side in behavior["leans"].items()]
    return sum(sides) / len(sides)


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


def _lean(high: bool) -> float:
    return LEANING if high else round(1 - LEANING, 2)
