"""NPC 생활·관계 규칙 데이터를 원본 스키마로 검사하고, 행동에 쓰는 사물을 장소별 사물 단어 배치와 대조한다.
설계는 docs/npc-life-design.md.

원본: docs/api/npc-ai-contracts/npc_life_rules_schemas.json (Unity·AI 워커·백엔드가 같이 읽는 계약)
"""

import json
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "docs/api/npc-ai-contracts/npc_life_rules_schemas.json"
WORLD_RULES = ROOT / "ai/npcs/rules/world-rules.json"  # ai/npcs 바로 아래는 NPC 설정 파일만 (npc_configs가 묶음)
OBJECT_WORD_BINDINGS = ROOT / "backend/src/main/resources/content/object-word-bindings.json"
LIFE_FOLDER = ROOT / "ai/npcs/life"
BEHAVIORS = ROOT / "ai/npcs/rules/behaviors.json"

_schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
WORLD_RULES_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/WorldRules"})
LIFE_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/NpcLife"})
BEHAVIOR_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/BehaviorLibrary"})


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


DAY_MINUTES = 24 * 60


def schedule_problems(schedule: list[dict]) -> list[str]:
    """일과표가 하루 24시간을 겹치지 않고 빈틈없이 채우는지. 하루를 분 단위로 칠해 비거나 두 번 칠해진 구간을 알려 준다.
    to가 from보다 이르면 자정을 넘기고, 같으면 하루 전체다."""
    covered = [0] * DAY_MINUTES
    for block in schedule:
        start = _minutes(block["from"])
        length = (_minutes(block["to"]) - start) % DAY_MINUTES or DAY_MINUTES
        for offset in range(length):
            covered[(start + offset) % DAY_MINUTES] += 1
    gaps = [f"{_clock(start)}~{_clock(end)}에 일과가 없음" for start, end in _runs(covered, lambda count: count == 0)]
    overlaps = [f"{_clock(start)}~{_clock(end)}에 일과가 겹침" for start, end in _runs(covered, lambda count: count > 1)]
    return gaps + overlaps


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


def life_problems(npc_folder: Path) -> list[str]:
    """NPC 폴더(ai/npcs)의 생활 파일(life/*.json)을 하나씩 검사하고, 인격 파일(*.json)과 짝이 맞는지 본다.
    스키마를 어긴 파일은 나머지를 믿을 수 없으니 스키마 오류만 알린다. 일과표의 장소는 사물 단어 배치의 장소 코드와 대조한다."""
    places = _placed_words().keys()
    problems, life_file_of, life_files = [], {}, set()
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
        problems += [f"{name}: {problem}" for problem in schedule_problems(life["schedule"])]
        problems += [
            f"{name} $.schedule[{i}].scene: 없는 장소 '{block['scene']}'"
            for i, block in enumerate(life["schedule"])
            if "scene" in block and block["scene"] not in places
        ]
        life_file_of[npc_id] = name

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


def _placed_words() -> dict[str, set[str]]:
    """장소 코드 → 그 장소에 배치된 사물 단어 키. Unity에서 내보낸 사물 단어 배치(OBJECT_WORD_BINDINGS)."""
    bindings = json.loads(OBJECT_WORD_BINDINGS.read_text(encoding="utf-8"))
    return {scene["sceneCode"]: set(scene["wordKeys"]) for scene in bindings["scenes"]}
