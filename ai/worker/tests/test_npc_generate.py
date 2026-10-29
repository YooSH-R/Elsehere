"""NPC 인격 카드 자동 생성: 재료 파일(ai/npcs/rules/persona-ingredients.json)의 검사와, 입력(npcId·MBTI·역할·장소)에서
규칙으로 인격 카드 뼈대를 만드는 생성기(ai/tools/npc_generate.py). 설계는 docs/npc-life-design.md."""

import copy
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_life_rules


def load_ingredients() -> dict:
    return json.loads(npc_life_rules.PERSONA_INGREDIENTS.read_text(encoding="utf-8"))


def changed(ingredients: dict, path: str, value) -> dict:
    """재료 파일에서 path(점으로 구분, 숫자는 목록 위치) 한 곳만 value로 바꾼 것. value가 None이면 그 칸을 뺀다."""
    data = copy.deepcopy(ingredients)
    *parents, key = [int(part) if part.isdigit() else part for part in path.split(".")]
    target = data
    for name in parents:
        target = target[name]
    if value is None:
        del target[key]
    else:
        target[key] = value
    return data


# ---------------------------------------------------------------------
# 재료 파일 검사
# ---------------------------------------------------------------------
def test_the_persona_ingredients_pass_their_schema_and_checks():
    ingredients = load_ingredients()

    assert npc_life_rules.ingredient_errors(ingredients) == []
    assert npc_life_rules.ingredient_problems(ingredients) == []


@pytest.mark.parametrize(
    ("path", "value", "where"),
    [
        ("names.0", "olivia", "$.names[0]"),  # 이름은 대문자로 시작하는 한 단어
        ("traits.0.leans", {"openness": "high", "extraversion": "high"}, "$.traits[0].leans"),  # 성격 특징은 축 하나
        ("occupations.0.place", None, "$.occupations[0]"),  # 직원(STAFF) 직업은 일하는 장소가 필요
        ("speechStyle.verbosity.axis", "kindness", "$.speechStyle.verbosity.axis"),
    ],
)
def test_an_ingredient_outside_the_schema_is_reported_where_it_is(path, value, where):
    errors = npc_life_rules.ingredient_errors(changed(load_ingredients(), path, value))

    assert where in [place for place, _ in errors]


def test_every_side_of_every_axis_needs_two_traits_to_choose_from():
    """성격 축마다 NPC가 기운 쪽의 특징을 하나 고르므로, 같은 성격끼리도 달라지게 양쪽에 두 개 이상."""
    ingredients = load_ingredients()
    ingredients["traits"] = [trait for trait in ingredients["traits"] if trait["leans"] != {"openness": "low"}]

    assert npc_life_rules.ingredient_problems(ingredients) == ["openness low 쪽 성격 특징이 2개 미만 (0개)"]


def test_an_occupation_age_must_be_one_of_the_age_ranges():
    ingredients = changed(load_ingredients(), "occupations.0.ages", ["teens"])

    assert npc_life_rules.ingredient_problems(ingredients) == ["$.occupations[0].ages: 나이대 목록에 없는 'teens'"]


def test_names_and_trait_names_are_used_only_once():
    ingredients = load_ingredients()
    ingredients["traits"][1]["name"] = ingredients["traits"][0]["name"]

    assert npc_life_rules.ingredient_problems(ingredients) == [
        f"$.traits[1]: 성격 특징 이름 '{ingredients['traits'][0]['name']}'를 $.traits[0]에서 이미 씀"
    ]
