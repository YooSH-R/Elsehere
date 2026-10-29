"""NPC 인격 카드 자동 생성: 재료 파일(ai/npcs/rules/persona-ingredients.json)의 검사와, 입력(npcId·MBTI·역할·장소)에서
규칙으로 인격 카드 뼈대를 만드는 생성기(ai/tools/npc_generate.py). 설계는 docs/npc-life-design.md."""

import asyncio
import copy
import json
import sys
from pathlib import Path

import httpx
import pytest

from app.config import Settings
from app.llm import LLM
from conftest import ok

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_configs
import npc_derive
import npc_generate
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


# ---------------------------------------------------------------------
# 규칙 뼈대 생성: 입력(npcId·MBTI·역할·장소)과 npcId 씨앗으로 재료를 골라 인격 카드를 만든다 (LLM 없음)
# ---------------------------------------------------------------------
def load_behaviors() -> dict:
    return json.loads(npc_life_rules.BEHAVIORS.read_text(encoding="utf-8"))


def generate(npc_id="GEN_001", mbti="INTJ-T", role="STAFF", place="CAFE", taken=(), ingredients=None, **fixed):
    """fixed: 고정할 칸(name·occupation·ageRange·values·motivations·interests). 나머지는 재료에서 고른다."""
    return npc_generate.generate(
        npc_id, role, place, ingredients or load_ingredients(), load_behaviors(), mbti=mbti, fixed=fixed, taken_names=set(taken)
    )


MBTIS = [e + n + f + j for e in "EI" for n in "NS" for f in "FT" for j in "JP"]
JOBS = [("STAFF", "CAFE"), ("STAFF", "HOTEL"), ("PASSERBY", "TOWN")]


def test_the_same_npc_id_always_makes_the_same_npc():
    assert generate("GEN_001") == generate("GEN_001")


def test_a_different_npc_id_makes_a_different_npc():
    assert generate("GEN_001")[0]["npcProfile"] != generate("GEN_002")[0]["npcProfile"]


@pytest.mark.parametrize("mbti", MBTIS)
@pytest.mark.parametrize(("role", "place"), JOBS)
def test_every_generated_npc_passes_the_shared_profile_schema(mbti, role, place):
    config, life = generate(f"GEN_{mbti}", f"{mbti}-A", role, place)

    assert npc_configs.schema_errors(config) == []
    assert npc_life_rules.life_errors(life) == []
    assert life == {"schemaVersion": "1.0", "npcId": f"GEN_{mbti}", "mbti": f"{mbti}-A", "role": role}


def test_a_generated_npc_is_inactive_until_someone_approves_it():
    """생성 결과는 초안이다. 사람이 보고 켜기 전까지 서비스에 나오지 않게 isActive false (목소리도 그때 정한다)."""
    config, _ = generate()

    assert config["isActive"] is False and config["voiceId"] is None and config["defaultLocationId"] == "CAFE"


@pytest.mark.parametrize("mbti", ["INTJ-T", "ESFP-A", "ENFJ"])
def test_traits_come_one_per_axis_from_the_side_the_npc_leans_to(mbti):
    """-T·-A가 없으면 신경성은 0.5(가운데)라 신경성 특징은 넣지 않는다."""
    dimensions = npc_derive.mbti_to_dimensions(mbti)
    by_name = {trait["name"]: trait["leans"] for trait in load_ingredients()["traits"]}

    traits = generate(mbti=mbti)[0]["npcProfile"]["personality"]["traits"]

    leans = [by_name[trait["name"]] for trait in traits]
    assert [axis for lean in leans for axis in lean] == [axis for axis in npc_derive.AXES if dimensions[axis] != 0.5]
    assert all(npc_generate.side(dimensions[axis]) == side for lean in leans for axis, side in lean.items())


def test_values_motivations_and_interests_never_come_from_the_other_side_or_role():
    dimensions = npc_derive.mbti_to_dimensions("INTJ-T")
    ingredients = load_ingredients()
    profile = generate(mbti="INTJ-T", role="STAFF")[0]["npcProfile"]

    for field, picked in [
        ("values", profile["values"]),
        ("motivations", profile["motivations"]),
        ("interests", profile["background"]["interests"]),
    ]:
        source = {item["text"]: item for item in ingredients[field]}
        for text in picked:
            item = source[text]
            assert all(npc_generate.side(dimensions[axis]) == side for axis, side in item.get("leans", {}).items()), (field, text)
            assert "STAFF" in item.get("roles", ["STAFF"]), (field, text)


def test_the_occupation_fits_the_role_and_place_and_the_age_fits_the_occupation():
    occupations = {occupation["text"]: occupation for occupation in load_ingredients()["occupations"]}

    for role, place in JOBS:
        profile = generate(role=role, place=place)[0]["npcProfile"]
        occupation = occupations[profile["occupation"]]

        assert occupation["role"] == role and occupation.get("place", place) == place
        assert profile["ageRange"] in occupation.get("ages", load_ingredients()["ageRanges"])


def test_a_name_already_used_by_another_npc_is_not_chosen():
    name = generate()[0]["npcProfile"]["name"]

    assert generate(taken=[name])[0]["npcProfile"]["name"] != name


def test_speech_and_social_behavior_follow_the_axes_and_role():
    """INTJ: 내향(말 짧게, 처음엔 짧게 인사), 사고형(건조하고 직설적), -T(긴장을 드러냄). 직원은 보통 격식."""
    rules = load_ingredients()
    profile = generate(mbti="INTJ-T", role="STAFF")[0]["npcProfile"]

    assert profile["speechStyle"] == {"tone": "quiet and dry", "formality": "neutral", "verbosity": "short", "directness": "direct"}
    assert profile["socialBehavior"] == {
        "firstMeeting": rules["socialBehavior"]["firstMeeting"]["low"],
        "familiarityExpression": rules["socialBehavior"]["familiarityExpression"]["low"],
        "conflictResponse": rules["socialBehavior"]["conflictResponse"]["high"],
        "boundaries": rules["socialBehavior"]["boundaries"]["STAFF"],
    }


def test_the_behavior_fragments_and_five_axes_are_added_like_the_handwritten_npcs():
    """손으로 쓴 NPC와 같은 자동 칸(성격 5축, npcId로 뽑은 행동 조각). 사연(배경 이야기)은 아직 비어 있다."""
    config, life = generate()
    profile = config["npcProfile"]

    assert profile == npc_derive.with_derived_traits(profile, life, load_behaviors())
    assert profile["behaviorRules"] == [
        b["text"] for b in npc_derive.pick_behaviors(npc_derive.mbti_to_dimensions("INTJ-T"), load_behaviors(), "GEN_001")
    ]
    assert profile["background"]["summary"] is None and profile["background"]["experiences"] == []


def test_without_an_mbti_one_is_chosen_from_the_npc_id():
    """어드민에서 MBTI를 비워 두면 npcId 씨앗으로 16유형과 -A·-T 중 하나를 고른다. 같은 id면 같은 MBTI."""
    config, life = generate(mbti=None)

    assert npc_derive.MBTI.match(life["mbti"]) and life["mbti"][-2:] in ("-A", "-T")
    assert generate(mbti=None)[1]["mbti"] == life["mbti"]
    assert config["npcProfile"]["personality"]["dimensions"] == {
        axis: npc_derive.mbti_to_dimensions(life["mbti"])[axis] for axis in npc_derive.AXES
    }


def test_fixed_items_are_kept_as_given_and_the_rest_is_chosen():
    fixed = {"name": "Zed", "occupation": "barista at the corner cafe", "ageRange": "40s", "interests": ["chess"]}
    profile = generate(**fixed)[0]["npcProfile"]

    assert (profile["name"], profile["occupation"], profile["ageRange"], profile["background"]["interests"]) == (
        "Zed",
        "barista at the corner cafe",
        "40s",
        ["chess"],
    )
    assert profile["values"] and profile["motivations"]  # 고정하지 않은 칸은 재료에서


def test_fixing_one_item_does_not_reshuffle_the_others():
    """칸마다 따로 씨앗을 써서, 이름만 고정해도 직업·관심사 같은 다른 칸은 그대로다."""
    free = generate()[0]["npcProfile"]
    named = generate(name="Zed")[0]["npcProfile"]

    assert named["name"] == "Zed"
    assert {**named, "name": free["name"]} == free


def test_a_fixed_occupation_from_the_candidates_keeps_its_age_ranges():
    occupation = next(job for job in load_ingredients()["occupations"] if job["text"] == "college student on a short break")

    profile = generate(role="PASSERBY", place="TOWN", occupation=occupation["text"])[0]["npcProfile"]

    assert profile["ageRange"] in occupation["ages"]


def test_candidates_from_the_request_replace_only_the_lists_they_give():
    """백엔드가 DB 후보를 보내면 그 목록만 바꾸고, 보내지 않은 목록과 말투·사회적 행동 규칙은 AI 기본 재료를 쓴다."""
    defaults = load_ingredients()
    merged = npc_generate.with_candidates(defaults, {"names": ["Zed"], "interests": [{"text": "chess"}]})

    assert merged["names"] == ["Zed"] and merged["interests"] == [{"text": "chess"}]
    assert {key: value for key, value in merged.items() if key not in ("names", "interests")} == {
        key: value for key, value in defaults.items() if key not in ("names", "interests")
    }
    profile = generate(ingredients=merged)[0]["npcProfile"]
    assert profile["name"] == "Zed" and profile["background"]["interests"] == ["chess"]


def test_a_staff_npc_needs_an_occupation_at_its_place():
    with pytest.raises(ValueError, match="SHOP"):
        generate(role="STAFF", place="SHOP")


def test_the_cli_writes_the_npc_and_life_files_and_skips_names_already_in_use(tmp_path):
    out = tmp_path / "drafts"
    service_names = {
        json.loads(path.read_text(encoding="utf-8"))["npcProfile"]["name"] for path in (npc_life_rules.ROOT / "ai/npcs").glob("*.json")
    }

    assert npc_generate.main(["GEN_001", "INTJ-T", "STAFF", "CAFE", "--out", str(out)]) == 0

    config = json.loads((out / "GEN_001.json").read_text(encoding="utf-8"))
    life = json.loads((out / "life/GEN_001.json").read_text(encoding="utf-8"))
    assert (config, life) == generate(taken=service_names)
    assert config["npcProfile"]["name"] not in service_names


# ---------------------------------------------------------------------
# LLM이 사연(배경 이야기·겪은 일)만 쓴다. 나머지 칸은 규칙으로 고른 그대로
# ---------------------------------------------------------------------
STORY = {
    "summary": "Henry grew up helping in his aunt's kitchen and still bakes the way she taught him.",
    "experiences": [
        {"event": "He once burned a whole batch on a busy morning.", "influence": "He now stays calm and starts over without fuss."}
    ],
}


def narrate(provider, model_output) -> tuple[dict, list[dict]]:
    """규칙 뼈대(GEN_001)를 가짜 공급자 LLM으로 다듬는다. (결과, 공급자가 받은 요청 본문들)."""
    settings = Settings(token="t", base_url="http://fake/v1", api_key="x", call_timeout_s=1.0, retry_pause_s=0.0, fallback={})
    sent = []

    def answer(body):
        sent.append(body)
        return ok(model_output if isinstance(model_output, str) else json.dumps(model_output))

    provider.answers["npc"] = answer

    async def run():
        llm = LLM(settings, httpx.MockTransport(provider.handler))
        try:
            return await npc_generate.narrate(generate()[0], llm)
        finally:
            await llm.aclose()

    return asyncio.run(run()), sent


def test_the_llm_writes_only_the_backstory(provider):
    skeleton = generate()[0]

    narrated, _ = narrate(provider, STORY)

    background = {**skeleton["npcProfile"]["background"], **STORY}
    assert narrated == {**skeleton, "npcProfile": {**skeleton["npcProfile"], "background": background}}


def test_the_llm_sees_the_card_and_is_told_not_to_invent_town_places(provider):
    profile = generate()[0]["npcProfile"]

    _, sent = narrate(provider, STORY)

    system, card = sent[0]["messages"][0]["content"], sent[0]["messages"][1]["content"]
    assert "backstory" in system and "places" in system
    assert profile["name"] in card and profile["personality"]["traits"][0]["name"] in card and profile["behaviorRules"][0] in card
    assert sent[0]["response_format"]["type"] == "json_schema"


@pytest.mark.parametrize(
    "story",
    [
        {**STORY, "summary": "x" * 1501},  # 스키마의 배경 이야기 상한
        {**STORY, "experiences": [{"event": " ", "influence": "Stays calm."}]},  # 빈 겪은 일
    ],
)
def test_a_backstory_outside_the_profile_schema_is_rejected(provider, story):
    with pytest.raises(ValueError, match="background"):
        narrate(provider, story)


def test_model_output_that_is_not_json_is_rejected(provider):
    with pytest.raises(ValueError, match="JSON"):
        narrate(provider, "Henry is a baker.")
