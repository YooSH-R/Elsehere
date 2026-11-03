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


# 행동 조각 뽑기: 성격 쪽에 맞는 조각을 NPC마다 고정된 결과로 뽑는다


def service_behaviors() -> dict:
    return json.loads(npc_life_rules.BEHAVIORS.read_text(encoding="utf-8"))


def leans_toward(behavior: dict, dimensions: dict) -> bool:
    return all((dimensions[axis] > 0.5) == (side == "high") and dimensions[axis] != 0.5 for axis, side in behavior["leans"].items())


MIA = npc_derive.mbti_to_dimensions("ESFJ-T")


def test_only_behaviors_on_the_npc_side_are_picked_one_per_category():
    picked = npc_derive.pick_behaviors(MIA, service_behaviors(), seed="CAFE_STAFF")

    assert len(picked) == npc_derive.BEHAVIORS_PER_NPC
    assert all(leans_toward(behavior, MIA) for behavior in picked)
    assert len({behavior["category"] for behavior in picked}) == len(picked)


def test_the_same_npc_always_gets_the_same_behaviors():
    """NPC id를 씨앗으로 써서, 실행할 때마다 성격이 바뀌지 않는다."""
    first = npc_derive.pick_behaviors(MIA, service_behaviors(), seed="CAFE_STAFF")

    assert npc_derive.pick_behaviors(MIA, service_behaviors(), seed="CAFE_STAFF") == first


def behavior(behavior_id: str, category: str, **extra) -> dict:
    return {"id": behavior_id, "text": "-", "ko": "-", "category": category, "leans": {"extraversion": "high"}, **extra}


def test_conflicting_behaviors_are_never_picked_together():
    library = {
        "behaviors": [
            behavior("loud", "habit", conflictsWith=["quiet"]),
            behavior("quiet", "social", conflictsWith=["loud"]),
            behavior("chatty", "speech"),
        ]
    }

    for seed in range(30):
        ids = {b["id"] for b in npc_derive.pick_behaviors(MIA, library, seed=str(seed))}
        assert not {"loud", "quiet"} <= ids


def test_fewer_behaviors_are_picked_when_few_fit():
    library = {"behaviors": [behavior("chatty", "speech"), {**behavior("shy", "social"), "leans": {"extraversion": "low"}}]}

    assert [b["id"] for b in npc_derive.pick_behaviors(MIA, library, seed="x")] == ["chatty"]


def test_rates_from_personality_and_behaviors_multiply():
    """성격에서 계산한 배수와 뽑힌 조각의 affects는 같은 모양이라 곱해서 합친다."""
    base = {"needRates": {"SOCIAL": 1.2}, "eventScale": {"RUDE": 1.16}}
    affects = [{"needRates": {"SOCIAL": 0.8, "FUN": 1.3}, "recognitionRate": 1.2}, {"eventScale": {"RUDE": 1.3}}]

    assert npc_derive.combine_rates(base, *affects) == {
        "needRates": {"SOCIAL": 0.96, "FUN": 1.3},
        "eventScale": {"RUDE": 1.51},
        "recognitionRate": 1.2,
    }


def test_salience_bonuses_add_up_to_at_most_two():
    """기억 세기는 1~3이라 더하는 값은 최대 2."""
    combined = npc_derive.combine_rates({"salienceBonus": {"TASK_DONE": 1}}, {"salienceBonus": {"TASK_DONE": 2}})

    assert combined == {"salienceBonus": {"TASK_DONE": 2}}


def load_life(npc_id: str) -> dict:
    return json.loads((npc_life_rules.LIFE_FOLDER / f"{npc_id}.json").read_text(encoding="utf-8"))


def home_of(npc_id: str) -> str:
    """인격 파일의 기본 장소."""
    config = json.loads((npc_life_rules.ROOT / "ai/npcs" / f"{npc_id}.json").read_text(encoding="utf-8"))
    return config["defaultLocationId"]


def life_values(npc_id: str) -> dict:
    return npc_derive.npc_life_values(load_life(npc_id), service_rules(), service_behaviors(), place=home_of(npc_id))


def test_an_npc_life_comes_from_its_mbti_and_npc_id():
    """성격 계산 → 행동 조각 뽑기(npcId가 씨앗) → 배수 합치기를 한 번에."""
    life = load_life("CAFE_STAFF")
    dimensions = npc_derive.mbti_to_dimensions(life["mbti"])
    behaviors = npc_derive.pick_behaviors(dimensions, service_behaviors(), seed="CAFE_STAFF")

    values = life_values("CAFE_STAFF")

    assert values["behaviors"] == [behavior["id"] for behavior in behaviors]
    assert values["rates"] == npc_derive.combine_rates(
        npc_derive.derive_rates(dimensions, service_rules()), *(behavior.get("affects", {}) for behavior in behaviors)
    )


def test_an_npc_schedule_comes_from_its_shift_in_its_own_place():
    """맡은 교대(카페 오픈 07:00~15:00)를 역할 규칙으로 하루 일과로 바꾸고 NPC의 기본 장소를 넣는다.
    NPC가 적은 지금은 자리를 비우지 않아(canLeave false) 스스로 채우지 못하는 욕구는 모두 부탁이 된다. 퇴근(OFF)에는 장소가 없다."""
    assert life_values("CAFE_STAFF")["schedule"] == [
        {"from": "07:00", "to": "11:00", "activity": "WORK", "scene": "CAFE", "canLeave": False},
        {"from": "11:00", "to": "11:30", "activity": "BREAK", "scene": "CAFE", "canLeave": False},
        {"from": "11:30", "to": "15:00", "activity": "WORK", "scene": "CAFE", "canLeave": False},
        {"from": "15:00", "to": "07:00", "activity": "OFF"},
    ]


def staff_day(start: str, end: str) -> list[dict]:
    return npc_derive.shift_day({"from": start, "to": end}, service_rules()["roles"]["STAFF"])


def day(*blocks: tuple[str, str, str]) -> list[dict]:
    return [{"from": start, "to": end, "activity": activity} for start, end, activity in blocks]


def test_a_long_storefront_shift_gets_one_break_in_the_middle():
    """빵집 06:00~18:00. 6시간보다 긴 교대는 가운데에 30분 쉬고, 교대 밖은 퇴근."""
    assert staff_day("06:00", "18:00") == day(
        ("06:00", "12:00", "WORK"), ("12:00", "12:30", "BREAK"), ("12:30", "18:00", "WORK"), ("18:00", "06:00", "OFF")
    )


def test_a_night_shift_runs_past_midnight():
    """호텔 야간 22:00~06:30. 가운데(02:15)에서 가장 가까운 30분 단위 시각 중 이른 쪽(02:00)에 쉰다."""
    assert staff_day("22:00", "06:30") == day(
        ("22:00", "02:00", "WORK"), ("02:00", "02:30", "BREAK"), ("02:30", "06:30", "WORK"), ("06:30", "22:00", "OFF")
    )


def test_the_break_starts_at_the_half_hour_nearest_the_middle_of_the_shift():
    assert staff_day("07:00", "15:20")[1]["from"] == "11:00"  # 가운데 11:10
    assert staff_day("07:00", "15:40")[1]["from"] == "11:30"  # 가운데 11:20


def test_a_shift_of_six_hours_or_less_has_no_break():
    assert staff_day("09:00", "15:00") == day(("09:00", "15:00", "WORK"), ("15:00", "09:00", "OFF"))


def test_a_whole_day_shift_has_no_time_off():
    """택시처럼 하루 전체(from == to)를 맡으면 퇴근이 없고, 가운데(12:00)에 쉰다."""
    assert staff_day("00:00", "00:00") == day(("00:00", "12:00", "WORK"), ("12:00", "12:30", "BREAK"), ("12:30", "00:00", "WORK"))


def test_a_passerby_is_free_while_out_in_town_and_takes_no_break():
    """행인의 교대는 마을에 나와 있는 시간대(마을 낮 09:00~18:00)다. 쉬는 시간 없이 자유 시간이고, 나머지는 집에 있다."""
    passerby = service_rules()["roles"]["PASSERBY"]

    assert npc_derive.shift_day({"from": "09:00", "to": "18:00"}, passerby) == day(("09:00", "18:00", "FREE"), ("18:00", "09:00", "OFF"))
    assert life_values("TOWN_PASSERBY")["schedule"] == [
        {"from": "09:00", "to": "18:00", "activity": "FREE", "scene": "TOWN", "canLeave": False},
        {"from": "18:00", "to": "09:00", "activity": "OFF"},
    ]


def test_a_storefront_clerk_keeps_its_home_place_while_working_its_storefront_hours():
    """거리 매장 직원의 기본 장소는 마을(TOWN)이고, 일과 시각은 자기 매장(빵집 06:00~18:00)의 교대에서 온다."""
    schedule = life_values("BAKERY_STAFF")["schedule"]

    assert [(block["from"], block["to"], block["activity"]) for block in schedule] == [
        ("06:00", "12:00", "WORK"),
        ("12:00", "12:30", "BREAK"),
        ("12:30", "18:00", "WORK"),
        ("18:00", "06:00", "OFF"),
    ]
    assert {block.get("scene") for block in schedule} == {"TOWN", None}


def test_service_npcs_stay_in_their_own_place_for_now():
    for npc_id in SERVICE_NPCS:
        scenes = {block["scene"] for block in life_values(npc_id)["schedule"] if "scene" in block}

        assert scenes == {home_of(npc_id)}, npc_id


def derived_from_life_file(npc_id: str) -> dict:
    """세 NPC의 최종 배수(성격 계산 × 뽑힌 행동 조각)."""
    return life_values(npc_id)["rates"]


def test_mia_keeps_her_persona_direction():
    """인격: 밝고 사람을 좋아함, 손님을 알아봄, 무례를 못 참음."""
    rates = derived_from_life_file("CAFE_STAFF")

    assert rates["needRates"]["SOCIAL"] > 1
    assert rates["recognitionRate"] > 1
    assert rates["eventScale"]["RUDE"] > 1


def test_ben_keeps_his_persona_direction():
    """인격: 단단하지만 정중하고 친절함, 무례한 말은 참지 않음."""
    rates = derived_from_life_file("TOWN_PASSERBY")

    assert rates["recognitionRate"] > 1
    assert rates["eventScale"]["RUDE"] > 1


def test_jake_keeps_his_persona_direction():
    """인격: 1년 넘게 일해 생기 없고 건성건성, 불친절, 호텔 업무 밖 이야기는 받지 않음."""
    rates = derived_from_life_file("HOTEL_STAFF")

    assert rates["needRates"]["FUN"] > 1  # 금방 지루해함
    assert rates["needRates"]["SOCIAL"] < 1
    assert rates["recognitionRate"] < 1
    assert rates["eventScale"]["SMALL_TALK"] < 1


def test_alex_keeps_the_persona_direction():
    """인격: 조용하고 꼼꼼함, 단골을 잘 기억함, 잡담은 짧게 받음."""
    rates = derived_from_life_file("SHOP_STAFF")

    assert rates["recognitionRate"] > 1
    assert rates["eventScale"]["SMALL_TALK"] < 1
    assert rates["needRates"]["SOCIAL"] < 1


def test_joe_keeps_the_persona_direction():
    """인격: 수다스럽고 밝음, 손님과의 잡담을 좋아함, 단골을 알아봄."""
    rates = derived_from_life_file("TAXI_DRIVER")

    assert rates["needRates"]["SOCIAL"] > 1
    assert rates["eventScale"]["SMALL_TALK"] > 1
    assert rates["recognitionRate"] > 1


def test_officer_dan_keeps_the_persona_direction():
    """인격: 침착하고 믿음직함, 원칙대로 일을 처리함, 잡담보다 용건을 먼저 들음."""
    rates = derived_from_life_file("POLICE_OFFICER")

    assert rates["eventScale"]["SMALL_TALK"] < 1
    assert rates["recognitionRate"] > 1


def test_postal_clerk_linda_keeps_the_persona_direction():
    """인격: 꼼꼼하고 원칙대로 처리함, 잡담보다 용건, 늘 하던 순서를 지킴."""
    rates = derived_from_life_file("POST_OFFICE_STAFF")

    assert rates["eventScale"]["SMALL_TALK"] < 1
    assert rates["needRates"]["FUN"] < 1


def test_diner_server_rosa_keeps_the_persona_direction():
    """인격: 다정하고 수다스러움, 단골을 반갑게 알아봄."""
    rates = derived_from_life_file("DINER_STAFF")

    assert rates["needRates"]["SOCIAL"] > 1
    assert rates["recognitionRate"] > 1


def test_bank_teller_henry_keeps_the_persona_direction():
    """인격: 정중하고 침착함, 무례에도 크게 흔들리지 않음, 잡담은 짧게."""
    rates = derived_from_life_file("BANK_STAFF")

    assert rates["eventScale"]["RUDE"] < 1
    assert rates["eventScale"]["SMALL_TALK"] < 1
    assert rates["needRates"]["SOCIAL"] < 1


def test_laundromat_owner_gus_keeps_the_persona_direction():
    """인격: 느긋하고 농담을 좋아함, 사과하면 금방 풀림."""
    rates = derived_from_life_file("LAUNDROMAT_STAFF")

    assert rates["needRates"]["FUN"] > 1
    assert rates["eventScale"]["APOLOGY"] > 1


def test_florist_lily_keeps_the_persona_direction():
    """인격: 다정하지만 수줍음, 손님을 잘 기억함."""
    rates = derived_from_life_file("FLOWER_SHOP_STAFF")

    assert rates["needRates"]["SOCIAL"] < 1
    assert rates["recognitionRate"] > 1


def test_barber_tony_keeps_the_persona_direction():
    """인격: 수다스럽고 장난기 많음, 먼저 말을 잘 검."""
    rates = derived_from_life_file("BARBER_STAFF")

    assert rates["needRates"]["SOCIAL"] > 1
    assert rates["needRates"]["FUN"] > 1
    assert rates["initiateTalkRate"] > 1


def test_baker_grace_keeps_the_persona_direction():
    """인격: 따뜻하고 부지런함, 손님을 반갑게 알아봄."""
    rates = derived_from_life_file("BAKERY_STAFF")

    assert rates["recognitionRate"] > 1
    assert rates["needRates"]["FUN"] < 1


def test_arcade_attendant_max_keeps_the_persona_direction():
    """인격: 활발한 게임광, 사람과 잡담을 좋아함."""
    rates = derived_from_life_file("ARCADE_STAFF")

    assert rates["needRates"]["SOCIAL"] > 1
    assert rates["needRates"]["FUN"] > 1
    assert rates["eventScale"]["SMALL_TALK"] > 1


def test_home_store_clerk_nina_keeps_the_persona_direction():
    """인격: 차분하고 센스 있음, 손님과 손님이 찾던 것을 잘 기억함."""
    rates = derived_from_life_file("HOME_STORE_STAFF")

    assert rates["needRates"]["SOCIAL"] < 1
    assert rates["recognitionRate"] > 1


# ---------------------------------------------------------------------
# 인격 카드에 자동 칸 더하기: 손으로 쓴 글은 그대로 두고 성격 5축과 뽑힌 행동 조각만 더한다
# ---------------------------------------------------------------------
MIA_LIFE = {"npcId": "CAFE_STAFF", "mbti": "ESFJ-T", "role": "STAFF"}
SERVICE_NPCS = [
    "ARCADE_STAFF",
    "BAKERY_STAFF",
    "BANK_STAFF",
    "BARBER_STAFF",
    "CAFE_STAFF",
    "DINER_STAFF",
    "FLOWER_SHOP_STAFF",
    "HOME_STORE_STAFF",
    "HOTEL_STAFF",
    "LAUNDROMAT_STAFF",
    "POLICE_OFFICER",
    "POST_OFFICE_STAFF",
    "SHOP_STAFF",
    "TAXI_DRIVER",
    "TOWN_PASSERBY",
]


def handwritten(rules: list[str]) -> dict:
    """손으로 쓴 인격 카드에서 자동 칸과 닿는 부분만."""
    return {"name": "Mia", "personality": {"traits": [{"name": "Bright", "description": "Cheerful."}]}, "behaviorRules": rules}


def picked_texts(mbti: str, npc_id: str) -> list[str]:
    behaviors = npc_derive.pick_behaviors(npc_derive.mbti_to_dimensions(mbti), service_behaviors(), seed=npc_id)
    return [behavior["text"] for behavior in behaviors]


def test_the_profile_gets_the_five_axes_from_its_mbti_in_schema_order():
    """인격 스키마(personality.dimensions)의 칸 순서(개방성·성실성·외향성·친화성·신경성)대로. 손으로 쓴 성격 특징은 그대로."""
    profile = npc_derive.with_derived_traits(handwritten([]), MIA_LIFE, service_behaviors())

    assert list(profile["personality"]["dimensions"].items()) == [
        ("openness", 0.25),
        ("conscientiousness", 0.75),
        ("extraversion", 0.75),
        ("agreeableness", 0.75),
        ("neuroticism", 0.7),
    ]
    assert profile["personality"]["traits"] == [{"name": "Bright", "description": "Cheerful."}]


def test_picked_behaviors_follow_the_handwritten_rules():
    profile = npc_derive.with_derived_traits(handwritten(["Repeats the order back."]), MIA_LIFE, service_behaviors())

    assert profile["behaviorRules"] == ["Repeats the order back.", *picked_texts("ESFJ-T", "CAFE_STAFF")]


def test_deriving_again_replaces_the_old_behaviors_instead_of_adding_more():
    """다시 계산하면 예전에 더한 조각을 빼고 새로 뽑은 조각을 더한다. 손으로 쓴 규칙은 남는다."""
    once = npc_derive.with_derived_traits(handwritten(["Repeats the order back."]), MIA_LIFE, service_behaviors())

    assert npc_derive.with_derived_traits(once, MIA_LIFE, service_behaviors()) == once
    changed = npc_derive.with_derived_traits(once, {**MIA_LIFE, "mbti": "ISTP-T"}, service_behaviors())
    assert changed["behaviorRules"] == ["Repeats the order back.", *picked_texts("ISTP-T", "CAFE_STAFF")]


def test_the_handwritten_profile_itself_is_not_changed():
    profile = handwritten(["Repeats the order back."])

    npc_derive.with_derived_traits(profile, MIA_LIFE, service_behaviors())

    assert profile == handwritten(["Repeats the order back."])


REDERIVE = "저장소 최상위에서 ai/.venv/Scripts/python ai/tools/npc_derive.py ai/npcs 로 인격 파일의 자동 칸을 다시 쓴다"


def test_the_service_npc_profiles_carry_their_derived_traits():
    """ai/npcs의 인격 파일은 생활 파일(MBTI)과 행동 조각으로 다시 계산한 것과 같아야 한다. 규칙을 고쳤으면 REDERIVE."""
    for npc_id in SERVICE_NPCS:
        config = json.loads((npc_life_rules.ROOT / "ai/npcs" / f"{npc_id}.json").read_text(encoding="utf-8"))

        assert config["npcProfile"] == npc_derive.with_derived_traits(config["npcProfile"], load_life(npc_id), service_behaviors()), (
            REDERIVE
        )


def test_the_cli_writes_the_derived_traits_only_into_npc_files_with_a_life_file(tmp_path):
    """생활 파일이 있는 NPC만 고쳐 쓰고, 없는 NPC 파일은 한 글자도 바꾸지 않는다."""
    (tmp_path / "rules").mkdir()
    (tmp_path / "life").mkdir()
    (tmp_path / "rules/behaviors.json").write_bytes(npc_life_rules.BEHAVIORS.read_bytes())
    (tmp_path / "life/CAFE_STAFF.json").write_text(json.dumps(MIA_LIFE), encoding="utf-8")
    mia = {"npcId": "CAFE_STAFF", "npcProfile": handwritten(["Repeats the order back."])}
    stranger = '{"npcId": "SHOP_STAFF", "npcProfile": {"name": "Alex"}}'
    (tmp_path / "CAFE_STAFF.json").write_text(json.dumps(mia), encoding="utf-8")
    (tmp_path / "SHOP_STAFF.json").write_text(stranger, encoding="utf-8")

    assert npc_derive.main([str(tmp_path)]) == 0

    written = json.loads((tmp_path / "CAFE_STAFF.json").read_text(encoding="utf-8"))
    assert written["npcProfile"] == npc_derive.with_derived_traits(mia["npcProfile"], MIA_LIFE, service_behaviors())
    assert (tmp_path / "SHOP_STAFF.json").read_text(encoding="utf-8") == stranger
