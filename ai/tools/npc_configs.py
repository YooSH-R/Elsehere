"""NPC 설정을 공통 인격 스키마 원본으로 검사한다.

원본: docs/api/npc-ai-contracts/npc_shared_schemas_v2.json의 NpcConfiguration (백엔드와 함께 쓰는 계약)
생성 모델(ai/worker/app/npc_profile_models.py)은 코드에서 꺼내 쓰기 위한 것이고, 스키마 규칙 일부
(활성 NPC의 voiceId 필수, 목록 중복 금지, 생략할 수 있는 칸의 null 금지, 글을 숫자·참거짓으로 바꿔 받지 않기)는
옮기지 못한다. 그래서 NPC 설정은 여기서 스키마 파일 그대로 한 번 더 검사한다.
"""

import json
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "docs/api/npc-ai-contracts/npc_shared_schemas_v2.json"

# 스키마 루트는 $defs 묶음일 뿐이라, 루트에서 NpcConfiguration 정의를 가리키게 해서 그 형식으로 검사한다
_schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
CONFIGURATION = Draft202012Validator({**_schema, "$ref": "#/$defs/NpcConfiguration"})


def schema_errors(config: dict) -> list[tuple[str, str]]:
    """스키마를 어긴 곳마다 (위치, 이유). 첫 오류에서 멈추지 않고 모두, 위치 순으로. 비어 있으면 통과."""
    errors = sorted(CONFIGURATION.iter_errors(config), key=lambda e: (e.json_path, e.message))
    return [(e.json_path, e.message) for e in errors]
