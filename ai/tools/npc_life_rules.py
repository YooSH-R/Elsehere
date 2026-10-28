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

_schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
WORLD_RULES_SCHEMA = Draft202012Validator({**_schema, "$ref": "#/$defs/WorldRules"})


def world_rules_errors(rules: dict) -> list[tuple[str, str]]:
    """스키마를 어긴 곳마다 (위치, 이유). 첫 오류에서 멈추지 않고 모두, 위치 순으로. 비어 있으면 통과."""
    errors = sorted(WORLD_RULES_SCHEMA.iter_errors(rules), key=lambda e: (e.json_path, e.message))
    return [(e.json_path, e.message) for e in errors]


def unplaced_actions(rules: dict) -> list[str]:
    """행동에 쓰는 사물이 그 장소에 배치된 사물 단어인지. 스키마로는 볼 수 없는, 다른 데이터와의 대조다.
    사물 단어 배치(OBJECT_WORD_BINDINGS)는 Unity에서 내보낸 장소별 사물 단어 목록이다."""
    bindings = json.loads(OBJECT_WORD_BINDINGS.read_text(encoding="utf-8"))
    placed = {scene["sceneCode"]: set(scene["wordKeys"]) for scene in bindings["scenes"]}
    return [
        f"$.actions[{i}]: {action['scene']}에 배치된 사물 단어가 아님 '{action['objectKey']}'"
        for i, action in enumerate(rules["actions"])
        if action["objectKey"] not in placed.get(action["scene"], set())
    ]
