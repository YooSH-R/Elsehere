"""NPC 하루를 LLM 없이 시뮬레이션해 규칙 숫자가 자연스러운지 본다. 설계는 docs/npc-life-design.md.

    ai/.venv/Scripts/python ai/tools/npc_simulate.py     서비스 NPC 3명의 하루 표

한 칸(tick)마다: 하던 행동이 끝나면 욕구를 채우고 → 쉬고 있으면 가장 급한 욕구를 채울 행동을 고르고 →
자리를 못 비우는데 스스로 못 채우는 욕구가 기준 아래면 부탁이 생기고 → 욕구가 시간만큼 줄어든다.
"""

import json
import math
import sys
from pathlib import Path

import npc_derive
import npc_life_rules

from app.npc_relationship import grow_recognition  # npc_derive가 같은 저장소 AI 워커를 경로에 넣는다

DAY_MINUTES = 24 * 60
MIN_SCORE = 1.0  # 이보다 덜 급하면 아무것도 하지 않고 기다린다


def simulate_day(npc: dict, rules: dict, requests: dict, tick: int = 10, tutorial_done: bool = True) -> dict:
    """npc: npcId·role·place·rates(npc_derive.npc_life_values의 rates)·schedule(장소가 들어간 일과), 튜토리얼 안내 NPC면 tutorialGuide.
    돌려주는 것: mode, needsAt(정시마다 욕구), actions(고른 행동과 시각), requests(생긴 부탁과 시각, 하루 한 번씩).
    학습자가 튜토리얼을 마치기 전(tutorial_done False)의 안내 NPC는 튜토리얼 모드: 정해진 흐름(Unity 대본)만 따르므로
    생활도 부탁도 하지 않는다."""
    if npc.get("tutorialGuide") and not tutorial_done:
        return {"mode": "TUTORIAL", "needsAt": {}, "actions": [], "requests": []}
    day = {"mode": "NORMAL", "needsAt": {}, "actions": [], "requests": []}
    needs, busy_until, doing, asked = None, None, None, set()
    for minute in range(0, DAY_MINUTES, tick):
        block = _block_at(npc["schedule"], minute)
        if block["activity"] == "OFF":  # 퇴근: 마을에 없음. 다시 나타나면 시작값부터
            needs, busy_until, doing = None, None, None
            continue
        if needs is None:
            needs = {name: rule["start"] for name, rule in rules["needs"].items()}
        if block["activity"] == "WORK":
            _eat_at_work(npc, minute, tick, needs, rules)
        if minute % 60 == 0:
            day["needsAt"][_clock(minute)] = {name: round(value, 1) for name, value in needs.items()}

        if doing and minute >= busy_until:
            _fill(needs, doing["fills"])
            doing = None
        allowed = _allowed_actions(npc, block, rules)
        if not doing:
            doing = _most_urgent(allowed, needs, rules["activities"][block["activity"]]["canFill"])
            if doing:
                busy_until = minute + doing["minutes"]
                day["actions"].append({"time": _clock(minute), "action": doing["action"]})

        for request in requests["requests"]:
            if request["id"] not in asked and _asks(request, npc, block, needs, allowed, rules):
                asked.add(request["id"])
                entry = {"time": _clock(minute), "id": request["id"]}
                if request["solve"] == "ERRAND":
                    entry["place"] = _errand_places(request, npc, rules)[0]
                day["requests"].append(entry)

        for name, rule in rules["needs"].items():
            rate = need_rate(npc, name)
            needs[name] = max(0.0, needs[name] - rule["dropPerHour"] * rate * tick / 60)
    return day


def need_rate(npc: dict, need: str) -> float:
    """욕구가 줄어드는 속도 배수: 성격 배수(rates.needRates) × 근무 형태 배수(needDrain, 퇴근 없는 NPC는 하루에 나눠 줆)."""
    return npc["rates"].get("needRates", {}).get(need, 1) * npc.get("needDrain", {}).get(need, 1)


def _allowed_actions(npc: dict, block: dict, rules: dict) -> list[dict]:
    """지금 장소에서, 이 역할이, 이 활동 중에 채울 수 있는 욕구를 채우는 행동."""
    can_fill = set(rules["activities"][block["activity"]]["canFill"])
    return [
        action
        for action in rules["actions"]
        if action["scene"] == block["scene"] and action.get("onlyRole", npc["role"]) == npc["role"] and can_fill & set(action["fills"])
    ]


def _most_urgent(actions: list[dict], needs: dict, can_fill: list[str]) -> dict | None:
    """점수 = Σ 채움 값 × 급함. 급함은 욕구가 낮을수록 가파르게 커진다((100 − 값)/100의 제곱). 점수가 같으면 먼저 적힌 행동."""

    def score(action: dict) -> float:
        return sum(amount * ((100 - needs[need]) / 100) ** 2 for need, amount in action["fills"].items() if need in can_fill)

    best = max(actions, key=score, default=None)
    return best if best and score(best) >= MIN_SCORE else None


def _asks(request: dict, npc: dict, block: dict, needs: dict, allowed: list[dict], rules: dict) -> bool:
    """욕구 계기만 본다(상황 계기는 학습자와 대화하는 시각·장소로 백엔드가 정한다).
    부탁하는 역할·활동이고, 자리를 못 비우고, 욕구가 기준 아래인데 지금 여기서 스스로 못 채우면 부탁한다.
    심부름은 자기 근무지가 아닌 가게에 그 욕구를 채우는 제공이 있을 때만(카페 직원이 카페 머핀을 부탁하지 않음)."""
    trigger = request["trigger"]
    if "need" not in trigger:
        return False
    need = trigger["need"]
    can_fill_here = any(need in action["fills"] for action in allowed)
    return (
        npc["role"] in request["roles"]
        and block["activity"] in request["activities"]
        and not block["canLeave"]
        and needs[need] < trigger["below"]
        and not can_fill_here
        and (request["solve"] != "ERRAND" or bool(_errand_places(request, npc, rules)))
    )


def _errand_places(request: dict, npc: dict, rules: dict) -> list[str]:
    """욕구 심부름의 해결처 후보: 그 욕구를 채우는 제공이 있는, 자기 근무지가 아닌 장소(이름 순)."""
    own = npc.get("workplace", npc["place"])
    need = request["trigger"]["need"]
    return [
        code
        for code, place in sorted(rules.get("places", {}).items())
        if code != own and any(need in offer.get("fills", {}) for offer in place.get("offers", []))
    ]


def _eat_at_work(npc: dict, minute: int, tick: int, needs: dict, rules: dict):
    """근무 중 짧은 끼니·커피: 그 칸에 끼니 시각이 들면 그 욕구를 시작값으로 되돌린다(이미 높으면 그대로)."""
    for meal in npc.get("meals", []):
        at = _minutes(meal["at"])
        if minute <= at < minute + tick:
            needs[meal["need"]] = max(needs[meal["need"]], rules["needs"][meal["need"]]["start"])


def _fill(needs: dict, fills: dict):
    for need, amount in fills.items():
        needs[need] = min(100.0, needs[need] + amount)


def _block_at(schedule: list[dict], minute: int) -> dict:
    for block in schedule:
        start, end = _minutes(block["from"]), _minutes(block["to"])
        if (minute - start) % DAY_MINUTES < ((end - start) % DAY_MINUTES or DAY_MINUTES):
            return block
    raise ValueError(f"{_clock(minute)}에 일과가 없음")


def _minutes(clock: str) -> int:
    hours, minutes = clock.split(":")
    return int(hours) * 60 + int(minutes)


def _clock(minutes: int) -> str:
    return f"{minutes // 60 % 24:02d}:{minutes % 60:02d}"


# 서비스 데이터와 관계·기억 곡선 (규칙 숫자가 의도대로인지 보는 용도)


def simulate_service_day(npc_id: str, tutorial_done: bool = True) -> dict:
    """저장소의 서비스 데이터(인격 파일의 기본 장소, 생활 파일의 MBTI·역할, 공통 규칙·행동 조각·부탁 틀)로 하루를 돌린다."""
    return simulate_day(service_npc(npc_id), _load(npc_life_rules.WORLD_RULES), _load(npc_life_rules.REQUESTS), tutorial_done=tutorial_done)


def service_npc(npc_id: str) -> dict:
    """서비스 NPC 하나의 시뮬레이션 입력: npcId·role·place와 npc_derive가 계산한 behaviors·rates·schedule."""
    life = _load(npc_life_rules.LIFE_FOLDER / f"{npc_id}.json")
    place = _load(npc_life_rules.ROOT / "ai/npcs" / f"{npc_id}.json")["defaultLocationId"]
    rules = _load(npc_life_rules.WORLD_RULES)
    values = npc_derive.npc_life_values(life, rules, _load(npc_life_rules.BEHAVIORS), place)
    return {
        "npcId": npc_id,
        "role": life["role"],
        "place": place,
        "workplace": life["workplace"],
        "meals": npc_derive.work_meals(npc_id, rules),
        "tutorialGuide": life.get("tutorialGuide", False),
        **values,
    }


def conversations_to_reach(threshold: float, rate: float, rules: dict) -> int:
    """정상 종료된 대화 몇 번째에 알아봄이 threshold에 닿는지. 대화마다 워커와 같은 규칙(grow_recognition)으로 오르고,
    백엔드처럼 정수로 저장한다."""
    recognition, conversations = 0, 0
    while recognition < threshold:
        conversations += 1
        recognition = round(grow_recognition(recognition, rate, rules))
    return conversations


def recall(salience: int, event_days: list[int], on_day: int, rules: dict) -> float:
    """event_days에 일어난 같은 일을 on_day에 떠올리는 정도(망각 곡선). 유지 기간은 인상 세기로 시작해
    날을 두고 다시 일어나면 spacedRepeatFactor배, 같은 날이면 sameDayFactor배가 된다."""
    memory = rules["memory"]
    stability, last = memory["stabilityDays"][str(salience)], event_days[0]
    for day in event_days[1:]:
        stability *= memory["spacedRepeatFactor"] if day - last >= 1 else memory["sameDayFactor"]
        last = day
    return math.exp(-(on_day - last) / stability)


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


SERVICE_NPCS = ["CAFE_STAFF", "HOTEL_STAFF", "TOWN_PASSERBY"]


def _request_text(request: dict) -> str:
    """예: 05:10 bring_food→DINER (심부름이면 해결처 장소를 붙인다)."""
    place = f"→{request['place']}" if "place" in request else ""
    return f"{request['time']} {request['id']}{place}"


def main() -> int:
    """서비스 NPC 3명의 하루(부탁·행동·정시 욕구)와 알아봄·기억 곡선을 표로 보여 준다."""
    rules = _load(npc_life_rules.WORLD_RULES)
    for npc_id in SERVICE_NPCS:
        npc, day = service_npc(npc_id), simulate_service_day(npc_id)
        counts: dict[str, int] = {}
        for entry in day["actions"]:
            counts[entry["action"]] = counts.get(entry["action"], 0) + 1
        print(f"\n== {npc_id} ({npc['role']}, {npc['place']}) 행동 조각: {', '.join(npc['behaviors'])}")
        if npc["tutorialGuide"]:
            print("튜토리얼 안내 NPC: 학습자가 튜토리얼을 마치기 전에는 정해진 흐름만 따른다. 아래는 마친 뒤의 하루")
        print("부탁:", ", ".join(_request_text(request) for request in day["requests"]) or "없음")
        print("행동:", ", ".join(f"{name}×{count}" for name, count in counts.items()) or "없음")
        print("시각  " + " ".join(f"{need:>6}" for need in rules["needs"]))
        for clock, needs in day["needsAt"].items():
            print(f"{clock} " + " ".join(f"{value:6.1f}" for value in needs.values()))

    relationship = rules["relationship"]
    print("\n== 알아봄 (정상 종료된 대화 몇 번째에)")
    for npc_id in SERVICE_NPCS:
        rate = service_npc(npc_id)["rates"].get("recognitionRate", 1)
        seen, regular = (conversations_to_reach(relationship[stage], rate, rules) for stage in ("seenBefore", "regular"))
        print(f"{npc_id} (×{rate}): 본 적 있음 {seen}번째, 단골 {regular}번째")

    print(f"\n== 기억 떠올림 (기준 {rules['memory']['recallAt']})")
    print(f"평범한 주문 한 번 → 다음 날 {recall(1, [0], 1, rules):.2f}, 이틀 연속 → 셋째 날 {recall(1, [0, 1], 2, rules):.2f}")
    print(f"무례 한 번 → 9일째 {recall(3, [0], 9, rules):.2f}, 10일째 {recall(3, [0], 10, rules):.2f}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows 터미널에서도 한글이 깨지지 않게
    sys.exit(main())
