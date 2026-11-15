"""ai/tools/npc_simulate.py: NPC 하루를 LLM 없이 시뮬레이션해 규칙 숫자가 자연스러운지 본다.
욕구가 줄고 → 가장 급한 욕구를 채울 행동을 고르고 → 자리를 못 비우는데 스스로 못 채우면 부탁이 생긴다. 설계는 docs/npc-life-design.md."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_life_rules
import npc_simulate

NO_DROP = {"dropPerHour": 0, "start": 70}


def rules(needs: dict, actions: list, can_fill: dict) -> dict:
    return {
        "needs": {name: needs.get(name, NO_DROP) for name in ["HUNGER", "ENERGY", "SOCIAL", "FUN", "WORK"]},
        "actions": actions,
        "activities": {
            activity: {"canFill": can_fill.get(activity, ["HUNGER", "ENERGY", "SOCIAL", "FUN"])} for activity in ["WORK", "BREAK", "FREE"]
        },
    }


def npc(place: str, activity: str, role: str = "STAFF", rates: dict | None = None) -> dict:
    """하루 종일 한 활동만 하는 NPC. 지금은 자리를 비우지 않는다."""
    return {
        "npcId": "TEST_NPC",
        "role": role,
        "place": place,
        "rates": rates or {},
        "schedule": [{"from": "00:00", "to": "00:00", "activity": activity, "scene": place, "canLeave": False}],
    }


def action(name: str, place: str, fills: dict, minutes: int = 10, **extra) -> dict:
    return {"objectKey": "thing", "scene": place, "action": name, "fills": fills, "minutes": minutes, **extra}


NO_REQUESTS = {"requests": []}


def test_needs_drop_at_the_npc_rate_while_present():
    day = npc_simulate.simulate_day(
        npc("TOWN", "FREE", rates={"needRates": {"HUNGER": 1.5}}), rules({"HUNGER": {"dropPerHour": 6, "start": 70}}, [], {}), NO_REQUESTS
    )

    assert day["needsAt"]["01:00"]["HUNGER"] == 61  # 70 − 6 × 1.5


def test_the_npc_does_what_fills_its_most_urgent_need():
    world = rules(
        {"HUNGER": {"dropPerHour": 0, "start": 20}, "ENERGY": {"dropPerHour": 0, "start": 60}},
        [action("REST", "TOWN", {"ENERGY": 40}), action("EAT", "TOWN", {"HUNGER": 40})],
        {},
    )

    day = npc_simulate.simulate_day(npc("TOWN", "FREE"), world, NO_REQUESTS)

    assert day["actions"][0] == {"time": "00:00", "action": "EAT"}


def test_while_working_only_work_is_done():
    world = rules(
        {"ENERGY": {"dropPerHour": 0, "start": 10}, "WORK": {"dropPerHour": 8, "start": 50}},
        [action("REST", "HOTEL", {"ENERGY": 40}), action("SERVE_GUEST", "HOTEL", {"WORK": 30}, onlyRole="STAFF")],
        {"WORK": ["WORK"]},
    )

    day = npc_simulate.simulate_day(npc("HOTEL", "WORK"), world, NO_REQUESTS)

    assert {entry["action"] for entry in day["actions"]} == {"SERVE_GUEST"}


def test_an_action_for_another_role_is_not_done():
    world = rules({"WORK": {"dropPerHour": 8, "start": 50}}, [action("SERVE_GUEST", "HOTEL", {"WORK": 30}, onlyRole="STAFF")], {})

    assert npc_simulate.simulate_day(npc("HOTEL", "FREE", role="PASSERBY"), world, NO_REQUESTS)["actions"] == []


HUNGRY_AT_WORK = {
    **rules({"HUNGER": {"dropPerHour": 6, "start": 35}}, [action("EAT", "CAFE", {"HUNGER": 40})], {"WORK": ["WORK"]}),
    "places": {"CAFE": {"offers": [{"key": "muffin", "fills": {"HUNGER": 40}}]}},
}
BRING_FOOD = {
    "requests": [
        {
            "id": "bring_food",
            "trigger": {"need": "HUNGER", "below": 30},
            "roles": ["STAFF"],
            "activities": ["WORK"],
            "solve": "ERRAND",
        }
    ]
}


def test_an_unmet_need_while_tied_to_the_post_becomes_a_request_once():
    """35에서 시간당 6씩 줄면 1시간째에 29로 기준(30) 아래. 하루에 한 번만. 그 욕구를 채우는 가게가 해결처다."""
    day = npc_simulate.simulate_day(npc("HOTEL", "WORK"), HUNGRY_AT_WORK, BRING_FOOD)

    assert day["requests"] == [{"time": "01:00", "id": "bring_food", "place": "CAFE"}]


def test_an_npc_does_not_ask_to_fetch_something_from_its_own_place():
    """카페 직원이 학습자에게 '카페 직원에게 머핀을 사다 달라'고 하지 않는다. 다른 가게에 먹을 것이 없으면 부탁하지 않는다."""
    assert npc_simulate.simulate_day(npc("CAFE", "WORK"), HUNGRY_AT_WORK, BRING_FOOD)["requests"] == []


def test_a_work_meal_brings_the_need_back_to_its_start():
    """근무 중에도 끼니 때는 시작값으로 되돌아간다(설계 7.5). 그 전에 기준 아래로 내려가면 부탁한다."""
    worker = {**npc("HOTEL", "WORK"), "meals": [{"at": "02:00", "need": "HUNGER"}]}
    world = {**HUNGRY_AT_WORK, "needs": {**HUNGRY_AT_WORK["needs"], "HUNGER": {"dropPerHour": 6, "start": 70}}}

    day = npc_simulate.simulate_day(worker, world, NO_REQUESTS)

    assert day["needsAt"]["02:00"]["HUNGER"] == 70
    assert day["needsAt"]["03:00"]["HUNGER"] == 64


def test_an_npc_off_duty_is_not_simulated():
    """퇴근(OFF) 동안에는 마을에 없으니 욕구 기록도 없다. 다시 나타나면 시작값부터."""
    off_until_noon = npc("TOWN", "FREE")
    off_until_noon["schedule"] = [
        {"from": "00:00", "to": "12:00", "activity": "OFF"},
        {"from": "12:00", "to": "00:00", "activity": "FREE", "scene": "TOWN", "canLeave": False},
    ]

    day = npc_simulate.simulate_day(off_until_noon, rules({"HUNGER": {"dropPerHour": 6, "start": 70}}, [], {}), NO_REQUESTS)

    assert "11:00" not in day["needsAt"]
    assert day["needsAt"]["12:00"]["HUNGER"] == 70


# 서비스 데이터로 숫자가 의도대로 움직이는지 (규칙 숫자를 바꾸면 여기서 드러난다)


def service_day(npc_id: str) -> dict:
    return npc_simulate.simulate_service_day(npc_id)


def test_staff_tied_to_the_post_ask_for_food_and_coffee_from_another_shop():
    """24시간 상주하는 Jake·Mia도 끼니 사이에는 배고프고 지쳐 다른 가게의 먹을 것·커피를 부탁한다. 자기 가게로는 보내지 않는다."""
    jake = service_day("HOTEL_STAFF")["requests"]
    mia = service_day("CAFE_STAFF")["requests"]

    assert {"bring_food", "bring_coffee"} <= {request["id"] for request in jake}
    assert "bring_food" in {request["id"] for request in mia}
    assert all(request.get("place") != "CAFE" for request in mia)
    assert all(request.get("place") != "HOTEL" for request in jake)


def test_a_resident_is_not_hungry_all_day():
    """끼니 리듬이 없으면 상주 직원은 하루 내내 0에 붙는다. 끼니 사이 몇 시간만 기준(40) 아래다."""
    needs = service_day("HOTEL_STAFF")["needsAt"]

    hungry = [clock for clock, values in needs.items() if values["HUNGER"] < 40]
    assert 0 < len(hungry) <= 12


def test_a_passerby_new_in_town_asks_the_learner_to_chat_in_the_afternoon():
    """마을에는 말 상대가 없어 Ben은 사교 욕구를 스스로 못 채우고, 오후에 학습자에게 구경할 곳을 묻는다."""
    ben = service_day("TOWN_PASSERBY")["requests"]

    assert [request["id"] for request in ben] == ["recommend_a_spot"]
    assert "13:00" <= ben[0]["time"] < "18:00"


def test_the_tutorial_guide_only_follows_the_fixed_flow_until_the_tutorial_is_done():
    """튜토리얼은 정해진 흐름(Unity 대본)대로 가야 해서, 학습자가 마치기 전까지 Ben은 생활도 부탁도 하지 않는다."""
    during = npc_simulate.simulate_service_day("TOWN_PASSERBY", tutorial_done=False)
    after = npc_simulate.simulate_service_day("TOWN_PASSERBY", tutorial_done=True)

    assert (during["mode"], during["actions"], during["requests"]) == ("TUTORIAL", [], [])
    assert after["mode"] == "NORMAL" and [request["id"] for request in after["requests"]] == ["recommend_a_spot"]


def test_other_npcs_live_as_usual_during_the_tutorial():
    mia = npc_simulate.simulate_service_day("CAFE_STAFF", tutorial_done=False)

    assert mia["mode"] == "NORMAL" and mia["actions"]


def service_rules() -> dict:
    return json.loads(npc_life_rules.WORLD_RULES.read_text(encoding="utf-8"))


def test_familiarity_grows_over_conversations_faster_for_friendly_npcs():
    """1차 유저 테스트 숫자(2026-10-30): 보통은 첫 대화 뒤 '본 적 있음', 넷째 대화 뒤 단골.
    친화성이 높으면(Mia 1.2) 빠르고, 낮으면(Jake 0.8) 느리다."""
    rules = service_rules()
    relationship = rules["relationship"]

    def until(stage: str, rate: float) -> int:
        return npc_simulate.conversations_to_reach(relationship[stage], rate, rules)

    assert (until("seenBefore", 1.0), until("regular", 1.0)) == (1, 4)
    assert (until("seenBefore", 1.2), until("regular", 1.2)) == (1, 3)
    assert (until("seenBefore", 0.8), until("regular", 0.8)) == (2, 4)


def test_an_everyday_order_is_remembered_after_a_couple_of_visits_and_rudeness_for_about_ten_days():
    rules = service_rules()
    recall_at = rules["memory"]["recallAt"]

    def recall(salience: int, event_days: list[int], on_day: int) -> float:
        return npc_simulate.recall(salience, event_days, on_day, rules)

    assert recall(1, [0], on_day=1) < recall_at  # 한 번 시킨 것은 다음 날 기억 못 함
    assert recall(1, [0, 1], on_day=2) >= recall_at  # 이틀 연속 시키면 셋째 날 기억
    assert recall(3, [0], on_day=9) >= recall_at  # 무례는 한 번이어도 9일째까지
    assert recall(3, [0], on_day=10) < recall_at


def test_the_command_line_prints_each_service_npc_day_and_the_curves(capsys):
    assert npc_simulate.main() == 0

    out = capsys.readouterr().out
    assert all(npc_id in out for npc_id in ["CAFE_STAFF", "HOTEL_STAFF", "TOWN_PASSERBY"])
    # Jake는 24시간 상주하므로 시뮬레이션 시작인 00:00부터 배고픔이 줄어, 아침 끼니 전에 다른 가게의 먹을 것을 부탁한다
    assert "05:10 bring_food→BAKERY" in out
    assert "본 적 있음" in out and "단골" in out
    assert "튜토리얼 안내 NPC" in out
