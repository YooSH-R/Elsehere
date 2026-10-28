"""NPC 하루를 LLM 없이 시뮬레이션해 규칙 숫자가 자연스러운지 본다. 설계는 docs/npc-life-design.md.

    ai/.venv/Scripts/python ai/tools/npc_simulate.py     서비스 NPC 3명의 하루 표

한 칸(tick)마다: 하던 행동이 끝나면 욕구를 채우고 → 쉬고 있으면 가장 급한 욕구를 채울 행동을 고르고 →
자리를 못 비우는데 스스로 못 채우는 욕구가 기준 아래면 부탁이 생기고 → 욕구가 시간만큼 줄어든다.
"""

DAY_MINUTES = 24 * 60
MIN_SCORE = 1.0  # 이보다 덜 급하면 아무것도 하지 않고 기다린다


def simulate_day(npc: dict, rules: dict, requests: dict, tick: int = 10) -> dict:
    """npc: npcId·role·place·rates(npc_derive.npc_life_values의 rates)·schedule(장소가 들어간 일과).
    돌려주는 것: needsAt(정시마다 욕구), actions(고른 행동과 시각), requests(생긴 부탁과 시각, 하루 한 번씩)."""
    day = {"needsAt": {}, "actions": [], "requests": []}
    needs, busy_until, doing, asked = None, None, None, set()
    for minute in range(0, DAY_MINUTES, tick):
        block = _block_at(npc["schedule"], minute)
        if block["activity"] == "OFF":  # 퇴근: 마을에 없음. 다시 나타나면 시작값부터
            needs, busy_until, doing = None, None, None
            continue
        if needs is None:
            needs = {name: rule["start"] for name, rule in rules["needs"].items()}
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
            if request["id"] not in asked and _asks(request, npc, block, needs, allowed):
                asked.add(request["id"])
                day["requests"].append({"time": _clock(minute), "id": request["id"]})

        for name, rule in rules["needs"].items():
            rate = npc["rates"].get("needRates", {}).get(name, 1)
            needs[name] = max(0.0, needs[name] - rule["dropPerHour"] * rate * tick / 60)
    return day


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


def _asks(request: dict, npc: dict, block: dict, needs: dict, allowed: list[dict]) -> bool:
    """부탁하는 역할·활동이고, 자리를 못 비우고, 욕구가 기준 아래인데 지금 여기서 스스로 못 채우면 부탁한다.
    자기 장소의 직원에게 다녀오라는 부탁(카페 직원이 카페 머핀을)은 하지 않는다."""
    own_place_errand = any(step["with"] == "staff" and step["place"] == npc["place"] for step in request["steps"])
    can_fill_here = any(request["need"] in action["fills"] for action in allowed)
    return (
        request["role"] == npc["role"]
        and block["activity"] in request["activities"]
        and not block["canLeave"]
        and needs[request["need"]] < request["below"]
        and not can_fill_here
        and not own_place_errand
    )


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
