"""ai/tools/npc_life_rules.py: NPC 생활·관계 규칙 데이터(Unity·AI 워커·백엔드가 같이 읽음)를 원본 스키마로 검사한다.
설계는 docs/npc-life-design.md."""

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_life_rules


def load_world_rules() -> dict:
    return json.loads(npc_life_rules.WORLD_RULES.read_text(encoding="utf-8"))


def changed(data: dict, path: tuple, value) -> dict:
    """data에서 path(키·순번 차례) 한 곳만 value로 바꾼다."""
    target = data
    for key in path[:-1]:
        target = target[key]
    target[path[-1]] = value
    return data


def rules_with(path: tuple, value) -> dict:
    """서비스 공통 규칙에서 한 곳만 바꾼 사본."""
    return changed(load_world_rules(), path, value)


def json_path(path: tuple) -> str:
    return "$" + "".join(f"[{key}]" if isinstance(key, int) else f".{key}" for key in path)


def broken_places(rules: dict) -> set[str]:
    return {where for where, _ in npc_life_rules.world_rules_errors(rules)}


def test_the_service_world_rules_pass_the_schema():
    assert npc_life_rules.world_rules_errors(load_world_rules()) == []


def test_actions_limit_who_can_do_them_by_role_not_by_npc():
    """NPC마다 손으로 적지 않는다: 직원만 하는 행동(커피 만들기 등)은 NPC id가 아니라 역할로 제한한다."""
    actions = load_world_rules()["actions"]

    assert not any("onlyFor" in action for action in actions)
    assert {action["action"] for action in actions if action.get("onlyRole") == "STAFF"} == {"MAKE_DRINK", "SERVE_GUEST"}


def test_every_need_has_a_drop_rate():
    """Unity가 욕구 다섯 개를 모두 시간에 따라 줄이므로, 하나라도 빠지면 막는다."""
    rules = load_world_rules()
    del rules["needs"]["FUN"]

    assert npc_life_rules.world_rules_errors(rules) == [("$.needs", "'FUN' is a required property")]


@pytest.mark.parametrize(
    ("path", "value", "where"),
    [
        (("schemaVersion",), "2.0", ("schemaVersion",)),
        (("needs", "HUNGER", "dropPerHour"), -1, None),  # 모든 욕구는 시간이 지나면 줄기만 한다 (낮을수록 급함)
        (("actions", 0, "objectKey"), "Coffee Machine", None),  # 사물 단어 키 형식
        (("actions", 0, "fills"), {"THIRST": 10}, None),  # 없는 욕구
        (("actions", 0, "fills", "WORK"), 0, None),  # 채우지 않는 행동은 적지 않음
        (("actions", 0, "minutes"), 0, None),
        (("actions", 0, "onlyRole"), "CHEF", None),  # 역할(백엔드 시드의 NPC 유형)만
        (("conversationEvents", "SHOUT"), {}, ("conversationEvents",)),  # 없는 사건
        (("conversationEvents", "RUDE", "mood", "ANGRY"), 2, ("conversationEvents", "RUDE", "mood")),  # 없는 감정
        (("conversationEvents", "RUDE", "memorySalience"), 4, None),  # 인상 세기는 1~3
        (("relationship", "affinityFollowRate"), 2, None),  # 비율은 0~1
        (("memory", "recallAt"), 1.5, None),
        (("derivation", 0, "target"), "speed", None),  # 계산할 수 있는 생활 값만
        (("derivation", 0, "axis"), "humor", None),
        (("roles", "CHEF"), {"schedule": []}, ("roles",)),  # 없는 역할 (백엔드 시드의 NPC 유형만)
        (("roles", "STAFF", "schedule"), [], None),
        (("roles", "STAFF", "schedule", 0, "from"), "8:00", None),  # HH:MM
        (("roles", "STAFF", "schedule", 0, "activity"), "SLEEP", None),
        (("activities", "WORK", "canFill"), ["THIRST"], ("activities", "WORK", "canFill", 0)),
        (("needs", "HUNGER", "start"), 101, None),
    ],
)
def test_values_the_rules_do_not_allow_are_reported_where_they_break(path, value, where):
    assert broken_places(rules_with(path, value)) == {json_path(where or path)}


def test_staff_only_work_while_working_and_look_after_themselves_otherwise():
    """근무 중에는 일만 해서, 자리를 못 비우는 직원의 다른 욕구는 부탁이 된다. 쉬는 시간·자유 시간에는 스스로 채운다."""
    activities = load_world_rules()["activities"]

    assert activities["WORK"]["canFill"] == ["WORK"]
    assert set(activities["BREAK"]["canFill"]) == set(activities["FREE"]["canFill"]) == {"HUNGER", "ENERGY", "SOCIAL", "FUN"}


def test_every_role_schedule_fills_the_whole_day_without_overlap():
    """NPC 일과는 손으로 적지 않고 역할별 틀에서 계산한다. 백엔드 시드의 NPC 유형과 같은 역할."""
    rules = load_world_rules()

    assert set(rules["roles"]) == {"STAFF", "PASSERBY"}
    assert npc_life_rules.role_schedule_problems(rules) == []


def test_a_gap_in_a_role_schedule_is_reported_with_the_role():
    rules = load_world_rules()
    rules["roles"]["STAFF"]["schedule"] = [block for block in rules["roles"]["STAFF"]["schedule"] if block["activity"] != "BREAK"]

    assert npc_life_rules.role_schedule_problems(rules) == ["roles.STAFF: 12:00~12:30에 일과가 없음"]


def test_the_service_actions_use_objects_placed_in_their_scene():
    assert npc_life_rules.unplaced_actions(load_world_rules()) == []


def test_an_object_not_placed_in_the_scene_is_a_problem():
    """사물 키는 그 장소에 배치된 사물 단어여야 학습자가 NPC가 쓰는 사물의 단어 카드를 볼 수 있다."""
    rules = rules_with(("actions", 0, "objectKey"), "pool")  # 카페에 없는 사물
    rules["actions"][1]["scene"] = "HOTEL"  # 커피 머신은 호텔에 없음

    assert npc_life_rules.unplaced_actions(rules) == [
        "$.actions[0]: CAFE에 배치된 사물 단어가 아님 'pool'",
        "$.actions[1]: HOTEL에 배치된 사물 단어가 아님 'coffee_machine'",
    ]


# NPC별 생활 파일 (ai/npcs/life/<npcId>.json, 인격 파일과 같은 npcId)
NPCS = npc_life_rules.ROOT / "ai/npcs"


def test_every_service_npc_has_a_life_file_without_problems():
    assert sorted(path.stem for path in npc_life_rules.LIFE_FOLDER.glob("*.json")) == ["CAFE_STAFF", "HOTEL_STAFF", "TOWN_PASSERBY"]
    assert npc_life_rules.life_problems(NPCS) == []


def load_life(npc_id: str) -> dict:
    return json.loads((npc_life_rules.LIFE_FOLDER / f"{npc_id}.json").read_text(encoding="utf-8"))


def life_with(path: tuple, value) -> dict:
    """Mia 생활 파일에서 한 곳만 바꾼 사본."""
    return changed(load_life("CAFE_STAFF"), path, value)


@pytest.mark.parametrize(
    ("path", "value", "where"),
    [
        (("schemaVersion",), "2.0", "$.schemaVersion"),
        (("npcId",), "cafe staff", "$.npcId"),  # 인격 파일·시드와 같은 대문자 코드
        (("mbti",), "ABCD", "$.mbti"),
        (("role",), "CHEF", "$.role"),  # 공통 규칙의 역할(백엔드 시드의 NPC 유형)만
        (("needRates",), {"SOCIAL": 1.2}, "$"),  # 손으로 적는 생활 값은 받지 않음 — 입력 노드에서 계산
        (("tutorialGuide",), "yes", "$.tutorialGuide"),
    ],
)
def test_values_a_life_file_does_not_allow_are_reported_where_they_break(path, value, where):
    errors = npc_life_rules.life_errors(life_with(path, value))

    assert {where for where, _ in errors} == {where}


def test_service_npc_life_files_hold_only_input_nodes():
    """NPC마다 정하는 것은 성격(MBTI)과 역할뿐. 생활 값과 일과는 공통 규칙으로 계산한다 (ai/tools/npc_derive.py)."""
    lives = {npc_id: load_life(npc_id) for npc_id in ["CAFE_STAFF", "HOTEL_STAFF", "TOWN_PASSERBY"]}

    assert {npc_id: (life["mbti"], life["role"]) for npc_id, life in lives.items()} == {
        "CAFE_STAFF": ("ESFJ-T", "STAFF"),
        "HOTEL_STAFF": ("ISTP-T", "STAFF"),
        "TOWN_PASSERBY": ("ESFJ-T", "PASSERBY"),
    }
    assert all(set(life) - {"tutorialGuide"} == {"schemaVersion", "npcId", "mbti", "role"} for life in lives.values())


def test_ben_is_the_only_tutorial_guide():
    """튜토리얼 안내 NPC는 학습자가 튜토리얼을 마치기 전까지 정해진 흐름으로만 움직인다 (튜토리얼 모드)."""
    guides = [npc_id for npc_id in ["CAFE_STAFF", "HOTEL_STAFF", "TOWN_PASSERBY"] if load_life(npc_id).get("tutorialGuide")]

    assert guides == ["TOWN_PASSERBY"]


def test_two_tutorial_guides_are_a_problem(tmp_path):
    configs = {"CAFE_STAFF.json": load_config("CAFE_STAFF"), "TOWN_PASSERBY.json": load_config("TOWN_PASSERBY")}
    lives = {"CAFE_STAFF.json": {**load_life("CAFE_STAFF"), "tutorialGuide": True}, "TOWN_PASSERBY.json": load_life("TOWN_PASSERBY")}

    assert npc_life_rules.life_problems(npc_folder(tmp_path, configs, lives)) == ["튜토리얼 안내 NPC가 둘 이상: CAFE_STAFF, TOWN_PASSERBY"]


STAFF_DAY = [
    {"from": "08:00", "to": "12:00", "activity": "WORK"},
    {"from": "12:00", "to": "12:30", "activity": "BREAK"},
    {"from": "12:30", "to": "17:00", "activity": "WORK"},
    {"from": "17:00", "to": "08:00", "activity": "OFF"},
]


def test_a_gap_in_the_schedule_is_a_problem():
    """비는 시간에는 Unity가 NPC를 어디에 둘지 모른다."""
    schedule = [block for block in STAFF_DAY if block["activity"] != "BREAK"]

    assert npc_life_rules.schedule_problems(schedule) == ["12:00~12:30에 일과가 없음"]


def test_overlapping_blocks_are_a_problem():
    schedule = [dict(block) for block in STAFF_DAY]
    schedule[1]["from"] = "11:30"  # 점심 휴식을 앞당겼는데 오전 근무를 그대로 둠

    assert npc_life_rules.schedule_problems(schedule) == ["11:30~12:00에 일과가 겹침"]


def test_a_block_can_run_past_midnight():
    whole_day = [{"from": "06:00", "to": "06:00", "activity": "OFF"}]
    night_and_day = [{"from": "22:00", "to": "06:00", "activity": "OFF"}, {"from": "06:00", "to": "22:00", "activity": "OFF"}]

    assert npc_life_rules.schedule_problems(whole_day) == []
    assert npc_life_rules.schedule_problems(night_and_day) == []


def load_config(npc_id: str) -> dict:
    return json.loads((NPCS / f"{npc_id}.json").read_text(encoding="utf-8"))


def npc_folder(tmp_path: Path, configs: dict[str, dict], lives: dict[str, dict]) -> Path:
    """configs[파일 이름] = 인격 설정, lives[파일 이름] = 생활 파일. ai/npcs와 같은 모양으로 쓴다."""
    (tmp_path / "life").mkdir()
    for name, config in configs.items():
        (tmp_path / name).write_text(json.dumps(config), encoding="utf-8")
    for name, life in lives.items():
        (tmp_path / "life" / name).write_text(json.dumps(life), encoding="utf-8")
    return tmp_path


def test_a_life_file_breaking_the_schema_is_reported_without_other_checks(tmp_path):
    """스키마를 어긴 파일은 짝 맞추기를 믿을 수 없으니 파일 이름을 붙인 스키마 오류만 알린다."""
    life = changed(load_life("CAFE_STAFF"), ("mbti",), "ABCD")
    folder = npc_folder(tmp_path, {"CAFE_STAFF.json": load_config("CAFE_STAFF")}, {"CAFE_STAFF.json": life})
    where, why = npc_life_rules.life_errors(life)[0]

    assert npc_life_rules.life_problems(folder) == [f"life/CAFE_STAFF.json {where}: {why}"]


def test_the_life_file_name_is_its_npc_id(tmp_path):
    folder = npc_folder(tmp_path, {"CAFE_STAFF.json": load_config("CAFE_STAFF")}, {"MIA.json": load_life("CAFE_STAFF")})

    assert npc_life_rules.life_problems(folder) == ["life/MIA.json: 파일 이름이 npcId 'CAFE_STAFF'와 다름"]


def test_every_npc_has_a_life_file_and_every_life_file_has_an_npc(tmp_path):
    configs = {"CAFE_STAFF.json": load_config("CAFE_STAFF"), "HOTEL_STAFF.json": load_config("HOTEL_STAFF")}
    lives = {"CAFE_STAFF.json": load_life("CAFE_STAFF"), "TOWN_PASSERBY.json": load_life("TOWN_PASSERBY")}

    assert npc_life_rules.life_problems(npc_folder(tmp_path, configs, lives)) == [
        "HOTEL_STAFF: 생활 파일이 없음 (life/HOTEL_STAFF.json)",
        "life/TOWN_PASSERBY.json: 인격 파일이 없는 npcId 'TOWN_PASSERBY'",
    ]


# 행동 조각 모음 (ai/npcs/rules/behaviors.json): 성격 축 쪽이 표시된 행동. 생성기가 NPC 성격에 맞는 조각을 뽑아 인격 글에 넣는다


def load_behaviors() -> dict:
    return json.loads(npc_life_rules.BEHAVIORS.read_text(encoding="utf-8"))


def test_the_service_behaviors_pass_the_schema_without_problems():
    library = load_behaviors()

    assert npc_life_rules.behavior_errors(library) == []
    assert npc_life_rules.behavior_problems(library) == []


@pytest.mark.parametrize(
    ("path", "value", "where"),
    [
        (("schemaVersion",), "2.0", None),
        (("behaviors", 0, "id"), "Talks To Strangers", None),
        (("behaviors", 0, "text"), "", None),
        (("behaviors", 0, "category"), "hobby", None),
        (("behaviors", 0, "leans"), {}, None),  # 어느 성격 쪽인지 없으면 뽑을 수 없음
        (("behaviors", 0, "leans"), {"humor": "high"}, None),  # 없는 성격 축
        (("behaviors", 0, "leans", "extraversion"), "very", None),
        (("behaviors", 0, "affects"), {"speed": 2}, None),  # 정해진 생활 값만 바꿀 수 있음
        (("behaviors", 0, "affects", "initiateTalkRate"), 0, None),
        (("behaviors", 2, "affects", "needRates"), {"THIRST": 0.8}, None),
        (("behaviors", 3, "affects", "eventScale"), {"SHOUT": 0.5}, None),
        (("behaviors", 7, "affects", "salienceBonus", "TASK_DONE"), 3, None),
        (("behaviors", 16, "affects", "memoryStabilityScale"), {"4": 1.5}, None),
        (("behaviors", 17, "conflictsWith"), [], None),
    ],
)
def test_values_a_behavior_does_not_allow_are_reported_where_they_break(path, value, where):
    errors = npc_life_rules.behavior_errors(changed(load_behaviors(), path, value))

    assert {where for where, _ in errors} == {json_path(where or path)}


def test_a_behavior_id_is_used_once():
    library = changed(load_behaviors(), ("behaviors", 1, "id"), "talks_to_strangers")

    assert npc_life_rules.behavior_problems(library) == ["$.behaviors[1]: id 'talks_to_strangers'를 $.behaviors[0]에서 이미 씀"]


def test_a_conflict_points_to_another_existing_behavior():
    library = changed(load_behaviors(), ("behaviors", 17, "conflictsWith"), ["calm_down"])
    changed(library, ("behaviors", 18, "conflictsWith"), ["shrugs_it_off"])  # 자기 자신

    assert npc_life_rules.behavior_problems(library) == [
        "$.behaviors[17].conflictsWith: 없는 조각 'calm_down'",
        "$.behaviors[18].conflictsWith: 자기 자신 'shrugs_it_off'",
    ]


# 부탁 퀘스트 틀 (ai/npcs/rules/requests.json): NPC가 스스로 채우지 못하는 욕구가 학습자에게 하는 부탁이 되고, 영어로 해결하는 퀘스트가 된다


def load_requests() -> dict:
    return json.loads(npc_life_rules.REQUESTS.read_text(encoding="utf-8"))


def test_the_service_requests_pass_the_schema_without_problems():
    library = load_requests()

    assert [request["id"] for request in library["requests"]] == ["bring_coffee", "bring_muffin", "recommend_a_spot"]
    assert npc_life_rules.request_errors(library) == []
    assert npc_life_rules.request_problems(library, load_world_rules()) == []


@pytest.mark.parametrize(
    ("path", "value", "where"),
    [
        (("requests", 0, "id"), "Bring Coffee", "$.requests[0].id"),
        (("requests", 0, "need"), "THIRST", "$.requests[0].need"),
        (("requests", 0, "below"), 100, "$.requests[0].below"),  # 다 찬 욕구로는 부탁하지 않음
        (("requests", 0, "role"), "CHEF", "$.requests[0].role"),
        (("requests", 0, "activities"), ["OFF"], "$.requests[0].activities[0]"),  # 퇴근한 NPC는 마을에 없음
        (("requests", 0, "steps"), [], "$.requests[0].steps"),
        (("requests", 0, "steps", 0, "languageFocus"), ["shouting"], "$.requests[0].steps[0].languageFocus[0]"),
        (("requests", 0, "reward", "fills"), {"THIRST": 10}, "$.requests[0].reward.fills"),
    ],
)
def test_values_a_request_does_not_allow_are_reported_where_they_break(path, value, where):
    errors = npc_life_rules.request_errors(changed(load_requests(), path, value))

    assert {where for where, _ in errors} == {where}


def test_a_staff_step_needs_a_place():
    """'그 장소의 직원에게 가서' 하는 단계라 장소가 없으면 학습자가 어디로 갈지 모른다."""
    library = load_requests()
    del library["requests"][0]["steps"][0]["place"]

    assert npc_life_rules.request_errors(library) == [("$.requests[0].steps[0]", "'place' is a required property")]


def test_a_request_must_be_solvable_and_askable():
    """들르는 장소의 직원이 그 욕구를 채울 수 있어야 하고, 부탁하는 역할의 일과에 그 활동이 있어야 한다."""
    library = load_requests()
    library["requests"][1]["steps"][0]["place"] = "HOTEL"  # 호텔에는 머핀이 없음
    library["requests"][2]["activities"] = ["WORK"]  # 행인 일과에는 근무가 없음

    assert npc_life_rules.request_problems(library, load_world_rules()) == [
        "$.requests[1].steps[0]: HOTEL에서는 HUNGER를 채울 수 없음",
        "$.requests[2]: PASSERBY 일과에 WORK가 없음",
    ]


def test_a_request_id_is_used_once():
    library = changed(load_requests(), ("requests", 1, "id"), "bring_coffee")

    assert npc_life_rules.request_problems(library, load_world_rules()) == ["$.requests[1]: id 'bring_coffee'를 $.requests[0]에서 이미 씀"]


def test_each_side_of_every_axis_has_enough_behaviors_to_draw_from():
    """성격이 어느 쪽이든 고를 조각이 있어야 같은 성격끼리도 조합이 달라진다."""
    library = load_behaviors()
    library["behaviors"] = [b for b in library["behaviors"] if b["leans"] != {"openness": "low"}]

    assert npc_life_rules.behavior_problems(library) == [f"openness low 쪽 조각이 {npc_life_rules.MIN_BEHAVIORS_PER_SIDE}개 미만 (0개)"]
