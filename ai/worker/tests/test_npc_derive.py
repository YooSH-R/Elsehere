"""ai/tools/npc_derive.py: NPC마다 손으로 값을 적지 않고, 입력 노드(MBTI·성격 5축)에서 공통 연결 규칙으로 생활 값을 계산한다.
설계는 docs/npc-life-design.md 10절."""

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_derive
import npc_life_rules


def service_rules() -> dict:
    return json.loads(npc_life_rules.WORLD_RULES.read_text(encoding="utf-8"))


def test_mbti_letters_become_the_five_axes():
    """E/I → 외향성, N/S → 개방성, F/T → 친화성, J/P → 성실성. 강도를 모르므로 끝값(0·1) 대신 0.75·0.25."""
    assert npc_derive.mbti_to_dimensions("ESFJ-T") == {
        "extraversion": 0.75,
        "openness": 0.25,
        "agreeableness": 0.75,
        "conscientiousness": 0.75,
        "neuroticism": 0.7,
    }


def test_the_assertive_or_missing_suffix_sets_neuroticism():
    """MBTI 네 글자에는 신경성이 없어 16Personalities식 -A/-T 꼬리로 받고, 없으면 중간값."""
    assert npc_derive.mbti_to_dimensions("ISTP-A")["neuroticism"] == 0.3
    assert npc_derive.mbti_to_dimensions("ISTP")["neuroticism"] == 0.5


def test_mbti_is_read_case_insensitively():
    assert npc_derive.mbti_to_dimensions("istp-t") == npc_derive.mbti_to_dimensions("ISTP-T")


@pytest.mark.parametrize("mbti", ["", "ESF", "ESFJX", "XSFJ", "ESFJ-Q", "ESFJ-"])
def test_a_string_that_is_not_mbti_is_rejected(mbti):
    with pytest.raises(ValueError, match="MBTI 형식이 아님"):
        npc_derive.mbti_to_dimensions(mbti)


def test_a_connection_rule_maps_an_axis_linearly_between_its_two_ends():
    """at0은 축이 0일 때, at1은 1일 때의 값. 그 사이는 직선."""
    rules = {"derivation": [{"target": "recognitionRate", "axis": "agreeableness", "at0": 0.5, "at1": 1.5}]}

    assert npc_derive.derive_rates({"agreeableness": 0.3}, rules) == {"recognitionRate": 0.8}


def test_the_service_rules_turn_mia_into_life_rates():
    """Mia(ESFJ-T): 외향 0.75·개방 0.25·친화 0.75·성실 0.75·신경 0.7. 결과는 행동 조각의 affects와 같은 모양(곱하는 배수)."""
    rates = npc_derive.derive_rates(npc_derive.mbti_to_dimensions("ESFJ-T"), service_rules())

    assert rates == {
        "needRates": {"SOCIAL": 1.2, "FUN": 0.8},
        "recognitionRate": 1.2,
        "eventScale": {"RUDE": 1.16, "SMALL_TALK": 0.8},
    }


def derived_from_life_file(npc_id: str) -> dict:
    life = json.loads((npc_life_rules.LIFE_FOLDER / f"{npc_id}.json").read_text(encoding="utf-8"))
    return npc_derive.derive_rates(npc_derive.mbti_to_dimensions(life["mbti"]), service_rules())


def test_mia_keeps_her_persona_direction():
    """인격: 밝고 사람을 좋아함, 손님을 알아봄, 무례를 못 참음."""
    rates = derived_from_life_file("CAFE_STAFF")

    assert rates["needRates"]["SOCIAL"] > 1
    assert rates["recognitionRate"] > 1
    assert rates["eventScale"]["RUDE"] > 1


def test_jake_keeps_his_persona_direction():
    """인격: 1년 넘게 일해 생기 없고 건성건성, 불친절, 호텔 업무 밖 이야기는 받지 않음."""
    rates = derived_from_life_file("HOTEL_STAFF")

    assert rates["needRates"]["FUN"] > 1  # 금방 지루해함
    assert rates["needRates"]["SOCIAL"] < 1
    assert rates["recognitionRate"] < 1
    assert rates["eventScale"]["SMALL_TALK"] < 1
