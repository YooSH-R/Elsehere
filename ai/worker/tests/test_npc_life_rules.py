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


def rules_with(path: tuple, value) -> dict:
    """서비스 공통 규칙에서 path(키·순번 차례) 한 곳만 value로 바꾼 사본."""
    rules = load_world_rules()
    target = rules
    for key in path[:-1]:
        target = target[key]
    target[path[-1]] = value
    return rules


def json_path(path: tuple) -> str:
    return "$" + "".join(f"[{key}]" if isinstance(key, int) else f".{key}" for key in path)


def broken_places(rules: dict) -> set[str]:
    return {where for where, _ in npc_life_rules.world_rules_errors(rules)}


def test_the_service_world_rules_pass_the_schema():
    assert npc_life_rules.world_rules_errors(load_world_rules()) == []


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
        (("actions", 0, "onlyFor"), [], None),
        (("conversationEvents", "SHOUT"), {}, ("conversationEvents",)),  # 없는 사건
        (("conversationEvents", "RUDE", "mood", "ANGRY"), 2, ("conversationEvents", "RUDE", "mood")),  # 없는 감정
        (("conversationEvents", "RUDE", "memorySalience"), 4, None),  # 인상 세기는 1~3
        (("relationship", "affinityFollowRate"), 2, None),  # 비율은 0~1
        (("memory", "recallAt"), 1.5, None),
    ],
)
def test_values_the_rules_do_not_allow_are_reported_where_they_break(path, value, where):
    assert broken_places(rules_with(path, value)) == {json_path(where or path)}


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
