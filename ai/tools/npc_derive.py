"""NPC 입력 노드(MBTI·성격 5축)에서 공통 연결 규칙으로 생활 값을 계산한다. 설계는 docs/npc-life-design.md 10절.

MBTI → 성격 5축, 행동 조각 뽑기, 인격 카드 자동 칸은 AI 워커의 app.npc_persona에, 성격 → 배수 계산은 app.npc_relationship에 있다
(워커 API와 함께 쓴다).

    ai/.venv/Scripts/python ai/tools/npc_derive.py ai/npcs   생활 파일이 있는 NPC 인격 파일의 자동 칸을 다시 쓴다
"""

import argparse
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))  # 인격 생성 규칙은 같은 저장소 AI 워커에 있다

# 다른 도구·테스트가 npc_derive에서 가져다 쓰는 이름도 함께 둔다
from app.npc_persona import AXES, BEHAVIORS_PER_NPC, MBTI, lean_fit, mbti_to_dimensions, pick_behaviors, with_derived_traits  # noqa: F401
from app.npc_relationship import combine_rates, derive_rates


def npc_life_values(life: dict, rules: dict, library: dict, place: str) -> dict:
    """생활 파일의 입력 노드(mbti·role·workplace·shift)와 npcId, 인격 파일의 기본 장소(place)에서 NPC 생활 값을 계산한다.
    - behaviors: 뽑힌 행동 조각 id. npcId를 씨앗으로 써서 같은 NPC는 늘 같은 결과다
    - rates: 성격 계산(derive_rates) × 뽑힌 조각 affects
    - schedule: 근무지에서 맡은 교대를 역할 규칙으로 하루 일과 틀로 바꾸고 기본 장소를 넣은 일과"""
    dimensions = mbti_to_dimensions(life["mbti"])
    behaviors = pick_behaviors(dimensions, library, seed=life["npcId"])
    rates = combine_rates(derive_rates(dimensions, rules), *(behavior.get("affects", {}) for behavior in behaviors))
    shift = rules["places"][life["workplace"]]["shifts"][life["shift"]]
    return {
        "behaviors": [behavior["id"] for behavior in behaviors],
        "rates": rates,
        "schedule": npc_schedule(shift_day(shift, rules["roles"][life["role"]]), place),
    }


def npc_schedule(template: list[dict], place: str) -> list[dict]:
    """역할의 일과 틀에 장소를 넣는다. NPC가 적은 지금은 각자 기본 장소 밖으로 나가지 않고 자리를 비우지 않는다
    (canLeave false → 스스로 채우지 못하는 욕구는 모두 부탁이 된다). 퇴근(OFF)에는 마을에 없으니 장소가 없다."""
    return [dict(block) if block["activity"] == "OFF" else {**block, "scene": place, "canLeave": False} for block in template]


DAY_MINUTES = 24 * 60


def shift_day(shift: dict, role: dict) -> list[dict]:
    """맡은 교대(from·to)를 역할 규칙(onShift·break)으로 하루 일과 틀로 바꾼다. 장소는 npc_schedule이 넣는다.
    교대 동안은 onShift 활동, 나머지는 퇴근(OFF)이다. 쉬는 시간이 있는 역할은 교대가 기준보다 길면 교대 가운데에 한 번 쉰다.
    to가 from보다 이르면 자정을 넘기고, 같으면 하루 전체라 퇴근이 없다."""
    start = _minutes(shift["from"])
    length = (_minutes(shift["to"]) - start) % DAY_MINUTES or DAY_MINUTES
    end = start + length
    break_rule = role.get("break")
    if break_rule and length > break_rule["whenShiftOverHours"] * 60:
        break_start = _nearest_half_hour(start + length / 2)
        break_end = break_start + break_rule["minutes"]
        blocks = [(start, break_start, role["onShift"]), (break_start, break_end, "BREAK"), (break_end, end, role["onShift"])]
    else:
        blocks = [(start, end, role["onShift"])]
    if length < DAY_MINUTES:
        blocks.append((end, start + DAY_MINUTES, "OFF"))
    return [{"from": _clock(block_start), "to": _clock(block_end), "activity": activity} for block_start, block_end, activity in blocks]


def _nearest_half_hour(minute: float) -> int:
    """30분 단위로 가장 가까운 시각(분). 딱 중간(15분·45분)이면 이른 쪽."""
    return math.ceil(minute / 30 - 0.5) * 30


def _minutes(clock: str) -> int:
    hours, minutes = clock.split(":")
    return int(hours) * 60 + int(minutes)


def _clock(minutes: int) -> str:
    return f"{minutes // 60 % 24:02d}:{minutes % 60:02d}"


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
