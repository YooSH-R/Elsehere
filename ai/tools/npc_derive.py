"""NPC 입력 노드(MBTI·성격 5축)에서 공통 연결 규칙으로 생활 값을 계산한다. 설계는 docs/npc-life-design.md 10절.

MBTI는 입력하기 편한 앞문이고, 계산과 인격 스키마(personality.dimensions)는 Big Five 5축(0~1)을 쓴다.
MBTI를 성격의 정답으로 다루지 않고 캐릭터 재료로만 쓴다.
"""

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


def _lean(high: bool) -> float:
    return LEANING if high else round(1 - LEANING, 2)
