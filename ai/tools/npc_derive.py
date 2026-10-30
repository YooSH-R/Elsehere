"""NPC 입력 노드(MBTI·성격 5축)에서 공통 연결 규칙으로 생활 값을 계산한다. 설계는 docs/npc-life-design.md 10절.

MBTI → 성격 5축, 행동 조각 뽑기, 인격 카드 자동 칸은 AI 워커의 app.npc_persona에, 성격 → 배수 계산은 app.npc_relationship에 있다
(워커 API와 함께 쓴다).

    ai/.venv/Scripts/python ai/tools/npc_derive.py ai/npcs   생활 파일이 있는 NPC 인격 파일의 자동 칸을 다시 쓴다
"""

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))  # 인격 생성 규칙은 같은 저장소 AI 워커에 있다

# 다른 도구·테스트가 npc_derive에서 가져다 쓰는 이름도 함께 둔다
from app.npc_persona import AXES, BEHAVIORS_PER_NPC, MBTI, lean_fit, mbti_to_dimensions, pick_behaviors, with_derived_traits  # noqa: F401
from app.npc_relationship import combine_rates, derive_rates


def npc_life_values(life: dict, rules: dict, library: dict, place: str) -> dict:
    """생활 파일의 입력 노드(mbti·role)와 npcId, 인격 파일의 기본 장소(place)에서 NPC 생활 값을 계산한다.
    - behaviors: 뽑힌 행동 조각 id. npcId를 씨앗으로 써서 같은 NPC는 늘 같은 결과다
    - rates: 성격 계산(derive_rates) × 뽑힌 조각 affects
    - schedule: 역할의 일과 틀에 기본 장소를 넣은 일과"""
    dimensions = mbti_to_dimensions(life["mbti"])
    behaviors = pick_behaviors(dimensions, library, seed=life["npcId"])
    rates = combine_rates(derive_rates(dimensions, rules), *(behavior.get("affects", {}) for behavior in behaviors))
    return {
        "behaviors": [behavior["id"] for behavior in behaviors],
        "rates": rates,
        "schedule": npc_schedule(rules["roles"][life["role"]]["schedule"], place),
    }


def npc_schedule(template: list[dict], place: str) -> list[dict]:
    """역할의 일과 틀에 장소를 넣는다. NPC가 적은 지금은 각자 기본 장소 밖으로 나가지 않고 자리를 비우지 않는다
    (canLeave false → 스스로 채우지 못하는 욕구는 모두 부탁이 된다). 퇴근(OFF)에는 마을에 없으니 장소가 없다."""
    return [dict(block) if block["activity"] == "OFF" else {**block, "scene": place, "canLeave": False} for block in template]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="생활 파일(life/<npcId>.json)이 있는 NPC 인격 파일에 성격 5축과 뽑힌 행동 조각을 다시 쓴다"
    )
    parser.add_argument("folder", type=Path, help="NPC 설정 폴더(ai/npcs). 행동 조각은 rules/behaviors.json, 생활 파일은 life/")
    args = parser.parse_args(argv)
    library = json.loads((args.folder / "rules/behaviors.json").read_text(encoding="utf-8"))
    for path in sorted(args.folder.glob("*.json")):
        config = json.loads(path.read_text(encoding="utf-8"))
        life_path = args.folder / "life" / f"{config['npcId']}.json"
        if not life_path.exists():  # 생활 파일이 없는 NPC는 손으로 쓴 그대로 둔다
            continue
        life = json.loads(life_path.read_text(encoding="utf-8"))
        derived = {**config, "npcProfile": with_derived_traits(config["npcProfile"], life, library)}
        if derived != config:
            # 셸의 > 대신 직접 쓴다: Windows PowerShell 5.1은 > 로 저장하면 BOM을 붙여 JSON 읽기가 깨진다
            path.write_text(json.dumps(derived, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
            print(f"자동 칸 다시 씀: {path.name}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows 터미널에서도 한글이 깨지지 않게
    sys.exit(main())
