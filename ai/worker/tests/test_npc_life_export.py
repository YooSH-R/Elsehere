"""ai/tools/npc_life_export.py: Unity가 NPC 생활(일과·욕구·사물 행동)을 돌릴 데이터를 AI 규칙 원본에서 내보낸다.
설계는 docs/npc-life-design.md 3·9절."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_derive
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


def test_an_npc_gets_its_role_place_and_the_schedule_derived_from_its_shift():
    """NPC마다 일과를 적지 않는다. 맡은 교대와 역할 규칙으로 계산한 일과를 시각과 활동만 옮긴다(장소는 NPC의 scene 하나)."""
    mia, derived = exported("CAFE_STAFF"), npc_simulate.service_npc("CAFE_STAFF")

    assert (mia["role"], mia["scene"]) == ("STAFF", "CAFE")
    assert mia["schedule"] == [{key: block[key] for key in ("from", "to", "activity")} for block in derived["schedule"]]


def test_need_rates_come_from_the_personality_and_default_to_one():
    """성격에서 계산한 배수가 없는 욕구는 1. Unity가 빠진 욕구를 따로 다루지 않게 다섯 욕구를 모두 채운다."""
    jake, derived = exported("HOTEL_STAFF"), npc_simulate.service_npc("HOTEL_STAFF")
    rates = derived["rates"].get("needRates", {})

    assert set(jake["needRates"]) == NEEDS
    assert jake["needRates"] == {need: rates.get(need, 1) for need in NEEDS}


def test_street_staff_keeps_its_workplace_separate_from_the_town_scene():
    assert exported("BANK_STAFF")["workplace"] == "BANK"
    assert exported("BANK_STAFF")["scene"] == "TOWN"


def test_shared_rules_are_copied_as_they_are_with_the_simulation_threshold():
    data, rules = npc_life_export.unity_life_data(), service_rules()

    assert (data["needs"], data["actions"], data["activities"]) == (rules["needs"], rules["actions"], rules["activities"])
    assert data["minScore"] == npc_simulate.MIN_SCORE


def test_the_unity_copy_matches_a_fresh_export():
    """규칙이나 생활 파일을 바꾸고 다시 내보내지 않으면 여기서 드러난다: ai/.venv/Scripts/python ai/tools/npc_life_export.py"""
    committed = json.loads(npc_life_export.UNITY_FILE.read_text(encoding="utf-8"))

    assert committed == npc_life_export.unity_life_data()


# 장소별 영업시간 (Unity가 가게 문을 열고 닫는 데 쓴다)


def test_place_hours_list_every_place_in_the_rules_order():
    places = npc_life_export.unity_place_hours()["places"]

    assert [place["code"] for place in places] == list(service_rules()["places"])


def test_every_service_place_is_exported_as_always_open():
    """Unity JsonUtility가 읽을 수 있게 24시간 영업 장소도 빈 시각 칸을 내보낸다."""
    places = npc_life_export.unity_place_hours()["places"]

    assert all(place["alwaysOpen"] and place["from"] == place["to"] == "" for place in places)


def test_a_place_with_hours_exports_its_time_window(tmp_path, monkeypatch):
    """서비스는 모두 24시간 영업이지만, 영업시간을 지정하는 데이터 계약은 유지한다."""
    rules = service_rules()
    rules["places"]["CAFE"]["hours"] = {"from": "07:00", "to": "21:00"}
    rules_file = tmp_path / "world-rules.json"
    rules_file.write_text(json.dumps(rules), encoding="utf-8")
    monkeypatch.setattr(npc_life_rules, "WORLD_RULES", rules_file)

    places = {place["code"]: place for place in npc_life_export.unity_place_hours()["places"]}

    assert places["CAFE"] == {"code": "CAFE", "alwaysOpen": False, "from": "07:00", "to": "21:00"}
    assert places["HOTEL"] == {"code": "HOTEL", "alwaysOpen": True, "from": "", "to": ""}


def test_the_unity_place_hours_match_a_fresh_export():
    """공통 규칙의 영업시간을 바꾸고 다시 내보내지 않으면 여기서 드러난다: ai/.venv/Scripts/python ai/tools/npc_life_export.py"""
    committed = json.loads(npc_life_export.PLACE_HOURS_FILE.read_text(encoding="utf-8"))

    assert committed == npc_life_export.unity_place_hours()
    assert committed["generatedBy"] == "ai/tools/npc_life_export.py"


def test_a_talk_with_the_learner_fills_social_because_no_object_does():
    """사교를 채우는 사물 행동이 없어, 학습자와 대화를 마치면 채운다. 다른 욕구는 대화로 채우지 않는다."""
    needs = service_rules()["needs"]

    assert not any("SOCIAL" in action["fills"] for action in service_rules()["actions"])
    assert needs["SOCIAL"]["conversationFill"] > 0
    assert [need for need, rule in needs.items() if rule.get("conversationFill")] == ["SOCIAL"]


def test_each_npc_gets_its_work_meals_derived_from_the_shared_rule():
    """근무 중 끼니·커피 시각은 NPC마다 적지 않고 공통 규칙에서 npcId 씨앗으로 계산해 옮긴다(설계 7.5)."""
    assert exported("HOTEL_STAFF")["meals"] == npc_derive.work_meals("HOTEL_STAFF", service_rules())


def test_the_backend_gets_offers_requests_and_favor_constants():
    """백엔드는 부탁을 고르고 판정할 때 장소 제공 목록·계기 틀·상수와 NPC 근무지·일과를 읽는다(설계 7.12)."""
    favors = npc_life_export.backend_favor_rules()
    rules = service_rules()

    assert favors["constants"] == rules["favors"]
    assert favors["affinityFollowRate"] == rules["relationship"]["affinityFollowRate"]
    assert set(favors["places"]) == {code for code, place in rules["places"].items() if "offers" in place}
    assert favors["places"]["BAKERY"]["name"] == {"en": "Maple Bakery", "ko": "베이커리"}
    assert [request["id"] for request in favors["requests"]][:3] == ["bring_food", "bring_coffee", "recommend_a_spot"]
    jake = next(npc for npc in favors["npcs"] if npc["npcId"] == "HOTEL_STAFF")
    assert (jake["role"], jake["workplace"]) == ("STAFF", "HOTEL")
    assert jake["schedule"] == exported("HOTEL_STAFF")["schedule"]


def test_the_backend_favor_rules_match_a_fresh_export():
    """계기 틀이나 제공 목록을 바꾸고 다시 내보내지 않으면 여기서 드러난다: ai/.venv/Scripts/python ai/tools/npc_life_export.py"""
    committed = json.loads(npc_life_export.BACKEND_FAVORS_FILE.read_text(encoding="utf-8"))

    assert committed == npc_life_export.backend_favor_rules()
