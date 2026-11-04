"""ai/tools/npc_life_export.py: Unity가 NPC 생활(일과·욕구·사물 행동)을 돌릴 데이터를 AI 규칙 원본에서 내보낸다.
설계는 docs/npc-life-design.md 3·9절."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_life_export
import npc_life_rules
import npc_simulate

NEEDS = {"HUNGER", "ENERGY", "SOCIAL", "FUN", "WORK"}


def service_rules() -> dict:
    return json.loads(npc_life_rules.WORLD_RULES.read_text(encoding="utf-8"))


def exported(npc_id: str) -> dict:
    return next(npc for npc in npc_life_export.unity_life_data()["npcs"] if npc["npcId"] == npc_id)


def test_every_npc_with_a_life_file_is_exported():
    exported_ids = {npc["npcId"] for npc in npc_life_export.unity_life_data()["npcs"]}

    assert exported_ids == {path.stem for path in npc_life_rules.LIFE_FOLDER.glob("*.json")}


def test_an_npc_gets_its_role_place_and_the_schedule_derived_from_its_role():
    """NPC마다 일과를 적지 않는다. 역할의 일과 틀에서 계산한 일과를 시각과 활동만 옮긴다(장소는 NPC의 scene 하나)."""
    mia, derived = exported("CAFE_STAFF"), npc_simulate.service_npc("CAFE_STAFF")

    assert (mia["role"], mia["scene"]) == ("STAFF", "CAFE")
    assert mia["schedule"] == [{key: block[key] for key in ("from", "to", "activity")} for block in derived["schedule"]]


def test_need_rates_come_from_the_personality_and_default_to_one():
    """성격에서 계산한 배수가 없는 욕구는 1. Unity가 빠진 욕구를 따로 다루지 않게 다섯 욕구를 모두 채운다."""
    jake, derived = exported("HOTEL_STAFF"), npc_simulate.service_npc("HOTEL_STAFF")
    rates = derived["rates"].get("needRates", {})

    assert set(jake["needRates"]) == NEEDS
    assert jake["needRates"] == {need: rates.get(need, 1) for need in NEEDS}


def test_shared_rules_are_copied_as_they_are_with_the_simulation_threshold():
    data, rules = npc_life_export.unity_life_data(), service_rules()

    assert (data["needs"], data["actions"], data["activities"]) == (rules["needs"], rules["actions"], rules["activities"])
    assert data["minScore"] == npc_simulate.MIN_SCORE


def test_the_unity_copy_matches_a_fresh_export():
    """규칙이나 생활 파일을 바꾸고 다시 내보내지 않으면 여기서 드러난다: ai/.venv/Scripts/python ai/tools/npc_life_export.py"""
    committed = json.loads(npc_life_export.UNITY_FILE.read_text(encoding="utf-8"))

    assert committed == npc_life_export.unity_life_data()
