"""Unity가 NPC 생활(일과·욕구·사물 행동)을 돌릴 데이터를 AI 규칙 원본에서 내보낸다. 설계는 docs/npc-life-design.md 3·9절.

    ai/.venv/Scripts/python ai/tools/npc_life_export.py     frontend/UnityClient/Assets/Data/Npc/NpcLife.json을 다시 쓴다

NPC마다 값을 적지 않는다. 생활 파일이 있는 NPC마다 입력 노드(MBTI·역할)에서 npc_derive가 계산한 일과·욕구 배수를 옮기고,
공통 규칙(욕구·사물 행동·활동별로 채울 수 있는 욕구)과 할 일 고르기 기준은 하루 시뮬레이션과 같은 값을 그대로 옮긴다.
공통 규칙의 장소별 영업시간은 frontend/UnityClient/Assets/Data/World/PlaceHours.json으로 함께 내보낸다.
"""

import json
import sys

import npc_life_rules
import npc_simulate

UNITY_FILE = npc_life_rules.ROOT / "frontend/UnityClient/Assets/Data/Npc/NpcLife.json"
PLACE_HOURS_FILE = npc_life_rules.ROOT / "frontend/UnityClient/Assets/Data/World/PlaceHours.json"


def unity_life_data() -> dict:
    rules = json.loads(npc_life_rules.WORLD_RULES.read_text(encoding="utf-8"))
    return {
        "generatedBy": "ai/tools/npc_life_export.py — 직접 고치지 않고 AI 규칙 원본을 고친 뒤 다시 내보냅니다",
        "minScore": npc_simulate.MIN_SCORE,
        "needs": rules["needs"],
        "actions": rules["actions"],
        "activities": rules["activities"],
        "npcs": [_npc(path.stem, rules) for path in sorted(npc_life_rules.LIFE_FOLDER.glob("*.json"))],
    }


def _npc(npc_id: str, rules: dict) -> dict:
    npc = npc_simulate.service_npc(npc_id)
    rates = npc["rates"].get("needRates", {})
    return {
        "npcId": npc_id,
        "role": npc["role"],
        "scene": npc["place"],
        "needRates": {need: rates.get(need, 1) for need in rules["needs"]},
        "schedule": [{key: block[key] for key in ("from", "to", "activity")} for block in npc["schedule"]],
    }


def unity_place_hours() -> dict:
    """장소마다 영업시간. 공통 규칙에 적힌 장소 순서 그대로다."""
    rules = json.loads(npc_life_rules.WORLD_RULES.read_text(encoding="utf-8"))
    return {
        "generatedBy": "ai/tools/npc_life_export.py",
        "places": [_place_hours(code, place) for code, place in rules["places"].items()],
    }


def _place_hours(code: str, place: dict) -> dict:
    """Unity JsonUtility는 없는 칸과 빈 칸을 구분하지 못해, 영업시간이 없는(24시간 여는) 곳도 같은 모양으로 두고 시각을 비운다."""
    if "hours" not in place:
        return {"code": code, "alwaysOpen": True, "from": "", "to": ""}
    return {"code": code, "alwaysOpen": False, "from": place["hours"]["from"], "to": place["hours"]["to"]}


def main() -> int:
    # 셸의 > 대신 직접 쓴다: Windows PowerShell 5.1은 > 로 저장하면 BOM을 붙여 JSON 읽기가 깨진다
    UNITY_FILE.write_text(json.dumps(unity_life_data(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"내보냄: {UNITY_FILE.relative_to(npc_life_rules.ROOT).as_posix()}")
    PLACE_HOURS_FILE.parent.mkdir(parents=True, exist_ok=True)
    PLACE_HOURS_FILE.write_text(json.dumps(unity_place_hours(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"내보냄: {PLACE_HOURS_FILE.relative_to(npc_life_rules.ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows 터미널에서도 한글이 깨지지 않게
    sys.exit(main())
