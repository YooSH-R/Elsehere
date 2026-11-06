"""NPC 생활·관계 규칙 데이터를 원본 스키마로 검사하고, 행동에 쓰는 사물을 장소별 사물 단어 배치와 대조한다.
장소별 영업시간·교대와 교대에서 계산한 하루 일과도 검사한다. 설계는 docs/npc-life-design.md.

원본: docs/api/npc-ai-contracts/npc_life_rules_schemas.json (Unity·AI 워커·백엔드가 같이 읽는 계약)
"""

import json
from pathlib import Path

import npc_derive
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "docs/api/npc-ai-contracts/npc_life_rules_schemas.json"
WORLD_RULES = ROOT / "ai/npcs/rules/world-rules.json"  # ai/npcs 바로 아래는 NPC 설정 파일만 (npc_configs가 묶음)
OBJECT_WORD_BINDINGS = ROOT / "backend/src/main/resources/content/object-word-bindings.json"
LIFE_FOLDER = ROOT / "ai/npcs/life"
BEHAVIORS = ROOT / "ai/npcs/rules/behaviors.json"
REQUESTS = ROOT / "ai/npcs/rules/requests.json"
PERSONA_INGREDIENTS = ROOT / "ai/npcs/rules/persona-ingredients.json"

_schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
WORLD_RULES_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/WorldRules"})
LIFE_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/NpcLife"})
BEHAVIOR_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/BehaviorLibrary"})
REQUEST_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/RequestLibrary"})
INGREDIENT_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/PersonaIngredients"})


def _errors(validator: Draft202012Validator, data: dict) -> list[tuple[str, str]]:
    """스키마를 어긴 곳마다 (위치, 이유). 첫 오류에서 멈추지 않고 모두, 위치 순으로. 비어 있으면 통과."""
    errors = sorted(validator.iter_errors(data), key=lambda e: (e.json_path, e.message))
    return [(e.json_path, e.message) for e in errors]


def world_rules_errors(rules: dict) -> list[tuple[str, str]]:
    return _errors(WORLD_RULES_SCHEMA, rules)


def unplaced_actions(rules: dict) -> list[str]:
    """행동에 쓰는 사물이 그 장소에 배치된 사물 단어인지. 스키마로는 볼 수 없는, 다른 데이터와의 대조다.
    사물 단어 배치(OBJECT_WORD_BINDINGS)는 Unity에서 내보낸 장소별 사물 단어 목록이다."""
    placed = _placed_words()
    return [
        f"$.actions[{i}]: {action['scene']}에 배치된 사물 단어가 아님 '{action['objectKey']}'"
        for i, action in enumerate(rules["actions"])
        if action["objectKey"] not in placed.get(action["scene"], set())
    ]


def life_errors(life: dict) -> list[tuple[str, str]]:
    return _errors(LIFE_SCHEMA, life)


def shift_day_problems(rules: dict) -> list[str]:
    """어느 역할이 어느 장소의 어느 교대를 맡아도, 교대와 역할 규칙으로 계산한 하루 일과가 24시간을 겹치지 않고 빈틈없이 채우는지."""
    problems = []
    for role_name, role in rules["roles"].items():
        for place_code, place in rules["places"].items():
            for shift_name, shift in place["shifts"].items():
                where = f"roles.{role_name} × places.{place_code}.shifts.{shift_name}"
                try:
                    schedule = npc_derive.shift_day(shift, role)
                except ValueError as error:
                    problems.append(f"{where}: {error}")
                    continue
                problems += [f"{where}: {problem}" for problem in schedule_problems(schedule)]
    return problems


def place_problems(rules: dict) -> list[str]:
    """영업시간이 있는 장소마다 문을 연 동안은 교대가 하나 이상 맡는지, 영업시간 밖에 걸친 교대가 없는지.
    영업시간이 없으면 24시간 여는 곳이라 교대가 하루를 다 채우지 않아도 된다(행인이 마을에 나와 있는 시간대 등)."""
    problems = []
    for code, place in rules["places"].items():
        if "hours" not in place:
            continue
        is_open = _painted([place["hours"]])
        staffed = _painted(place["shifts"].values())
        unstaffed = [open_count > 0 and staff_count == 0 for open_count, staff_count in zip(is_open, staffed)]
        problems += [f"places.{code}: {_clock(start)}~{_clock(end)}에 맡은 교대가 없음" for start, end in _runs(unstaffed, bool)]
        for name, shift in place["shifts"].items():
            after_hours = [shift_count > 0 and open_count == 0 for shift_count, open_count in zip(_painted([shift]), is_open)]
            problems += [
                f"places.{code}.shifts.{name}: {_clock(start)}~{_clock(end)}은 영업시간 밖" for start, end in _runs(after_hours, bool)
            ]
    return problems


def understaffed(lives: list[dict], rules: dict, workplace: str, span: dict, at_least: int) -> list[str]:
    """span(from·to) 동안 그 근무지에서 맡은 교대 중인 NPC(생활 파일의 근무지·교대로 셈, 쉬는 시간 포함)가
    at_least명보다 적은 시간. 마을(TOWN)은 나와 있는 행인 수다."""
    shifts = rules["places"][workplace]["shifts"]
    on_shift = _painted(
        {"from": "00:00", "to": "00:00"} if life.get("resident", False) else shifts[life["shift"]]
        for life in lives
        if life["workplace"] == workplace
    )
    short = [inside > 0 and count < at_least for inside, count in zip(_painted([span]), on_shift)]
    return [f"{workplace}: {_clock(start)}~{_clock(end)}에 교대 중인 NPC가 {at_least}명보다 적음" for start, end in _runs(short, bool)]


def working_staff_gaps(lives: list[dict], rules: dict, workplace: str) -> list[str]:
    """휴식 중인 직원을 제외하고 실제 응대할 직원이 없는 영업 시간을 찾는다."""
    place = rules["places"][workplace]
    working = []
    for life in lives:
        if life["workplace"] != workplace or life["role"] != "STAFF":
            continue
        schedule = npc_derive.life_day(life, rules)
        working.extend(block for block in schedule if block["activity"] == "WORK")
    opened = _painted([place.get("hours", {"from": "00:00", "to": "00:00"})])
    staffed = _painted(working)
    gaps = [open_count > 0 and count == 0 for open_count, count in zip(opened, staffed)]
    return [f"{workplace}: {_clock(start)}~{_clock(end)}에 응대할 직원이 없음" for start, end in _runs(gaps, bool)]


DAY_MINUTES = 24 * 60


def schedule_problems(schedule: list[dict]) -> list[str]:
    """일과표가 하루 24시간을 겹치지 않고 빈틈없이 채우는지. 하루를 분 단위로 칠해 비거나 두 번 칠해진 구간을 알려 준다.
    to가 from보다 이르면 자정을 넘기고, 같으면 하루 전체다."""
    covered = _painted(schedule)
    gaps = [f"{_clock(start)}~{_clock(end)}에 일과가 없음" for start, end in _runs(covered, lambda count: count == 0)]
    overlaps = [f"{_clock(start)}~{_clock(end)}에 일과가 겹침" for start, end in _runs(covered, lambda count: count > 1)]
    return gaps + overlaps


def _painted(spans) -> list[int]:
    """시간 구간(from·to)들을 하루에 분 단위로 칠해, 분마다 몇 번 칠해졌는지. to가 from보다 이르면 자정을 넘기고, 같으면 하루 전체다."""
    covered = [0] * DAY_MINUTES
    for span in spans:
        start = _minutes(span["from"])
        length = (_minutes(span["to"]) - start) % DAY_MINUTES or DAY_MINUTES
        for offset in range(length):
            covered[(start + offset) % DAY_MINUTES] += 1
    return covered


def _runs(covered: list[int], hit) -> list[list[int]]:
    """hit인 분이 이어진 구간들 [시작, 끝). 자정을 넘는 구간은 하나로 합친다."""
    runs, start = [], None
    for minute in range(DAY_MINUTES + 1):
        inside = minute < DAY_MINUTES and hit(covered[minute])
        if inside and start is None:
            start = minute
        elif not inside and start is not None:
            runs.append([start, minute])
            start = None
    if len(runs) > 1 and runs[0][0] == 0 and runs[-1][1] == DAY_MINUTES:
        runs[0][0] = runs.pop()[0]
    return runs


def _minutes(clock: str) -> int:
    hours, minutes = clock.split(":")
    return int(hours) * 60 + int(minutes)


def _clock(minutes: int) -> str:
    return f"{minutes // 60 % 24:02d}:{minutes % 60:02d}"


def life_problems(npc_folder: Path, rules: dict) -> list[str]:
    """NPC 폴더(ai/npcs)의 생활 파일(life/*.json)을 하나씩 검사하고, 인격 파일(*.json)과 짝이 맞는지,
    근무지·교대가 공통 규칙(rules)의 places에 있는지 본다. 스키마를 어긴 파일은 나머지를 믿을 수 없으니 스키마 오류만 알린다."""
    problems, life_file_of, life_files, guides = [], {}, set(), []
    for path in sorted((npc_folder / "life").glob("*.json")):
        name = f"life/{path.name}"
        life_files.add(path.stem)
        life = json.loads(path.read_text(encoding="utf-8"))
        errors = life_errors(life)
        if errors:
            problems += [f"{name} {where}: {why}" for where, why in errors]
            continue
        npc_id = life["npcId"]
        if path.stem != npc_id:
            problems.append(f"{name}: 파일 이름이 npcId '{npc_id}'와 다름")
        workplace = rules["places"].get(life["workplace"])
        if workplace is None:
            problems.append(f"{name}: 공통 규칙 places에 없는 근무지 '{life['workplace']}'")
        elif life["shift"] not in workplace["shifts"]:
            problems.append(f"{name}: {life['workplace']}에 없는 교대 '{life['shift']}'")
        if life.get("tutorialGuide"):
            guides.append(npc_id)
        life_file_of[npc_id] = name

    if len(guides) > 1:  # 튜토리얼은 정해진 흐름 하나라 안내 NPC도 하나
        problems.append(f"튜토리얼 안내 NPC가 둘 이상: {', '.join(guides)}")

    npc_ids = [json.loads(path.read_text(encoding="utf-8"))["npcId"] for path in sorted(npc_folder.glob("*.json"))]
    problems += [
        f"{npc_id}: 생활 파일이 없음 (life/{npc_id}.json)" for npc_id in npc_ids if npc_id not in life_file_of and npc_id not in life_files
    ]
    problems += [f"{name}: 인격 파일이 없는 npcId '{npc_id}'" for npc_id, name in life_file_of.items() if npc_id not in npc_ids]
    return problems


def behavior_errors(library: dict) -> list[tuple[str, str]]:
    return _errors(BEHAVIOR_SCHEMA, library)


AXES = ("openness", "conscientiousness", "extraversion", "agreeableness", "neuroticism")
MIN_BEHAVIORS_PER_SIDE = 2  # 성격이 어느 쪽이든 고를 조각이 있어야 같은 성격끼리도 조합이 달라진다


def behavior_problems(library: dict) -> list[str]:
    """행동 조각끼리의 대조: id는 한 번만, 충돌 표시는 다른 조각을 가리킴, 성격 축 양쪽마다 조각이 충분한지."""
    behaviors = library["behaviors"]
    problems, first_at = [], {}
    for i, behavior in enumerate(behaviors):
        if behavior["id"] in first_at:
            problems.append(f"$.behaviors[{i}]: id '{behavior['id']}'를 $.behaviors[{first_at[behavior['id']]}]에서 이미 씀")
        else:
            first_at[behavior["id"]] = i
    for i, behavior in enumerate(behaviors):
        for other in behavior.get("conflictsWith", []):
            if other == behavior["id"]:
                problems.append(f"$.behaviors[{i}].conflictsWith: 자기 자신 '{other}'")
            elif other not in first_at:
                problems.append(f"$.behaviors[{i}].conflictsWith: 없는 조각 '{other}'")
    for axis in AXES:
        for side in ("high", "low"):
            count = sum(1 for behavior in behaviors if behavior["leans"].get(axis) == side)
            if count < MIN_BEHAVIORS_PER_SIDE:
                problems.append(f"{axis} {side} 쪽 조각이 {MIN_BEHAVIORS_PER_SIDE}개 미만 ({count}개)")
    return problems


def ingredient_errors(ingredients: dict) -> list[tuple[str, str]]:
    return _errors(INGREDIENT_SCHEMA, ingredients)


MIN_TRAITS_PER_SIDE = 2  # 성격 축마다 기운 쪽에서 하나씩 고르므로, 같은 성격끼리도 달라지게 두 개 이상


def ingredient_problems(ingredients: dict) -> list[str]:
    """재료끼리의 대조: 성격 특징 이름은 한 번만, 성격 축 양쪽마다 특징이 충분한지, 직업의 나이대가 나이대 목록에 있는지."""
    traits = ingredients["traits"]
    problems, first_at = [], {}
    for i, trait in enumerate(traits):
        if trait["name"] in first_at:
            problems.append(f"$.traits[{i}]: 성격 특징 이름 '{trait['name']}'를 $.traits[{first_at[trait['name']]}]에서 이미 씀")
        else:
            first_at[trait["name"]] = i
    for axis in AXES:
        for side in ("high", "low"):
            count = sum(1 for trait in traits if trait["leans"] == {axis: side})
            if count < MIN_TRAITS_PER_SIDE:
                problems.append(f"{axis} {side} 쪽 성격 특징이 {MIN_TRAITS_PER_SIDE}개 미만 ({count}개)")
    ages = set(ingredients["ageRanges"])
    for i, occupation in enumerate(ingredients["occupations"]):
        problems += [f"$.occupations[{i}].ages: 나이대 목록에 없는 '{age}'" for age in occupation.get("ages", []) if age not in ages]
    return problems


def request_errors(library: dict) -> list[tuple[str, str]]:
    return _errors(REQUEST_SCHEMA, library)


def request_problems(library: dict, rules: dict) -> list[str]:
    """부탁 퀘스트 틀과 공통 규칙의 대조: id는 한 번만, 들르는 장소에서 그 욕구를 채울 수 있는지,
    부탁하는 역할의 일과에 그 활동이 있는지."""
    problems, first_at = [], {}
    for i, request in enumerate(library["requests"]):
        if request["id"] in first_at:
            problems.append(f"$.requests[{i}]: id '{request['id']}'를 $.requests[{first_at[request['id']]}]에서 이미 씀")
            continue
        first_at[request["id"]] = i
        for j, step in enumerate(request["steps"]):
            if step["with"] == "staff" and not _can_fill_at(rules, step["place"], request["need"]):
                problems.append(f"$.requests[{i}].steps[{j}]: {step['place']}에서는 {request['need']}를 채울 수 없음")
        role_activities = _role_activities(rules["roles"][request["role"]])
        problems += [
            f"$.requests[{i}]: {request['role']} 일과에 {activity}가 없음"
            for activity in request["activities"]
            if activity not in role_activities
        ]
    return problems


def _role_activities(role: dict) -> set[str]:
    """역할의 하루에 나올 수 있는 활동: 교대 중 활동, 쉬는 시간이 있으면 BREAK, 교대 밖 퇴근."""
    return {role["onShift"], "OFF"} | ({"BREAK"} if "break" in role else set())


def _can_fill_at(rules: dict, place: str, need: str) -> bool:
    return any(action["scene"] == place and need in action["fills"] for action in rules["actions"])


def _placed_words() -> dict[str, set[str]]:
    """장소 코드 → 그 장소에 배치된 사물 단어 키. Unity에서 내보낸 사물 단어 배치(OBJECT_WORD_BINDINGS)."""
    bindings = json.loads(OBJECT_WORD_BINDINGS.read_text(encoding="utf-8"))
    return {scene["sceneCode"]: set(scene["wordKeys"]) for scene in bindings["scenes"]}
