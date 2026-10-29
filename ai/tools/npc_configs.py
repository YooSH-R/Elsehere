"""NPC 설정을 공통 인격 스키마 원본으로 검사하고, NPC마다 하나인 설정 파일을 백엔드 전달용 배열로 합친다.

    ai/.venv/Scripts/python ai/tools/npc_configs.py <NPC 설정 폴더>                               검사만
    ai/.venv/Scripts/python ai/tools/npc_configs.py <NPC 설정 폴더> --out npc_configurations.json 검사하고 합친 배열 저장

원본: docs/api/npc-ai-contracts/npc_shared_schemas_v2.json의 NpcConfiguration·NpcConfigurationList (백엔드와 함께 쓰는 계약)
생성 모델(ai/worker/app/npc_profile_models.py)은 코드에서 꺼내 쓰기 위한 것이고, 스키마 규칙 일부
(활성 NPC의 voiceId 필수, 목록 중복 금지, 생략할 수 있는 칸의 null 금지, 글을 숫자·참거짓으로 바꿔 받지 않기)는
옮기지 못한다. 그래서 NPC 설정은 여기서 스키마 파일 그대로 한 번 더 검사한다.
문제가 하나라도 있으면 배열을 저장하지 않고 문제 목록만 오류 출력으로 내며 실패 코드 1로 끝난다.
"""

import argparse
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator
from pydantic import ValidationError

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))  # 상황은 같은 저장소 AI 워커의 추론 요청 모델로 검사한다

from app.npc_respond import Scenario

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "docs/api/npc-ai-contracts/npc_shared_schemas_v2.json"

_schema = json.loads(SCHEMA.read_text(encoding="utf-8"))


def _validator(definition: str) -> Draft202012Validator:
    """스키마 루트는 $defs 묶음일 뿐이라, 루트에서 정의 하나를 가리키게 해서 그 형식으로 검사한다."""
    return Draft202012Validator({**_schema, "$ref": f"#/$defs/{definition}"})


CONFIGURATION = _validator("NpcConfiguration")
CONFIGURATION_LIST = _validator("NpcConfigurationList")  # NPC 수(1~1000) 같은 배열 규칙


def _errors(validator: Draft202012Validator, data) -> list[tuple[str, str]]:
    errors = sorted(validator.iter_errors(data), key=lambda e: (e.json_path, e.message))
    return [(e.json_path, e.message) for e in errors]


def schema_errors(config: dict) -> list[tuple[str, str]]:
    """스키마를 어긴 곳마다 (위치, 이유). 첫 오류에서 멈추지 않고 모두, 위치 순으로. 비어 있으면 통과."""
    return _errors(CONFIGURATION, config)


def bundle(folder: Path) -> tuple[list[dict], list[str]]:
    """폴더의 NPC 설정 파일(*.json)을 이름 순으로 하나씩 검사하고 백엔드 전달용 배열로 합친다.

    (합친 배열, 문제 목록)을 돌려준다. 문제가 하나라도 있으면 배열을 넘기지 않는다.
    npcId 중복은 스키마를 통과한 파일끼리 본다 (스키마를 어긴 파일은 npcId부터 틀렸을 수 있어서).
    """
    configs, problems, file_of = [], [], {}
    for path in sorted(folder.glob("*.json")):
        try:
            config = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            problems.append(f"{path.name}: JSON 형식이 아님 (줄 {e.lineno}, 칸 {e.colno})")
            continue
        errors = schema_errors(config)
        if errors:
            problems += [f"{path.name} {where}: {why}" for where, why in errors]
            continue
        npc_id = config["npcId"]
        if npc_id in file_of:
            problems.append(f"{path.name}: npcId '{npc_id}'를 {file_of[npc_id]}에서 이미 씀")
            continue
        file_of[npc_id] = path.name
        configs.append(config)
    if not problems:  # 파일마다 통과했으면 남은 것은 배열 규칙(NPC 수)뿐
        problems += [f"합친 배열 {where}: {why}" for where, why in _errors(CONFIGURATION_LIST, configs)]
    return configs, problems


SERVICE_SCENARIOS = "scenarios/service_scenarios.json"  # npcId → 서비스에서 쓰는 상황 파일 이름 (scenarios/<이름>.json)


def service_scenarios(folder: Path, configs: list[dict]) -> tuple[dict[str, dict], list[str]]:
    """NPC마다 서비스 대화에서 쓸 상황(추론 계약의 scenario)을 npcId로 모은다. 백엔드가 추론 요청에 그대로 담아 보낸다.

    (npcId → 상황, 문제 목록)을 돌려준다. 상황은 인격과 따로 관리하므로(설계 기준) 설정 파일이 아니라 연결 파일로 잇는다.
    """
    links = json.loads((folder / SERVICE_SCENARIOS).read_text(encoding="utf-8"))
    scenarios, problems = {}, []
    for config in configs:
        npc_id = config["npcId"]
        if npc_id not in links:
            problems.append(f"{npc_id}: 서비스 상황 연결이 없음 ({SERVICE_SCENARIOS})")
            continue
        scenario, scenario_problems = _scenario(folder, links[npc_id], npc_id)
        if scenario_problems:
            problems += scenario_problems
        elif scenario["locationId"] != config["defaultLocationId"]:
            problems.append(f"{npc_id}: 상황 장소 '{scenario['locationId']}'이 NPC 기본 장소 '{config['defaultLocationId']}'와 다름")
        else:
            scenarios[npc_id] = scenario
    npc_ids = {config["npcId"] for config in configs}
    problems += [f"{SERVICE_SCENARIOS}: 설정 파일에 없는 npcId '{npc_id}'" for npc_id in links if npc_id not in npc_ids]
    return scenarios, problems


def _scenario(folder: Path, name: str, npc_id: str) -> tuple[dict | None, list[str]]:
    """상황 파일 하나를 Worker 추론 API가 받는 형식(app.npc_respond.Scenario)으로 검사한다."""
    path = folder / "scenarios" / f"{name}.json"
    if not path.exists():
        return None, [f"{npc_id}: 상황 파일이 없음 (scenarios/{name}.json)"]
    scenario = json.loads(path.read_text(encoding="utf-8"))
    try:
        Scenario.model_validate(scenario)
    except ValidationError as e:
        return None, [f"scenarios/{name}.json {'.'.join(str(x) for x in err['loc'])}: {err['msg']}" for err in e.errors()]
    return scenario, []


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="NPC 설정 파일을 검사하고, --out을 주면 백엔드 전달용 배열(JSON)로 합쳐 저장한다")
    parser.add_argument("folder", type=Path, help="NPC 설정 파일(*.json) 폴더. NPC마다 파일 하나")
    parser.add_argument("--out", type=Path, help="합친 배열을 저장할 파일. 없으면 검사만")
    parser.add_argument("--scenarios-out", type=Path, help=f"NPC별 서비스 상황(npcId → scenario)을 저장할 파일. 연결은 {SERVICE_SCENARIOS}")
    args = parser.parse_args(argv)
    if not args.folder.is_dir():
        parser.error(f"폴더가 없음: {args.folder}")

    configs, problems = bundle(args.folder)
    scenarios = {}
    if args.scenarios_out and not problems:
        scenarios, problems = service_scenarios(args.folder, configs)
    if problems:  # 하나라도 틀리면 어느 파일도 쓰지 않는다 (백엔드가 반만 맞는 데이터를 읽지 않게)
        print("\n".join(problems), file=sys.stderr)
        return 1
    print(f"NPC {len(configs)}명 검사 통과: {', '.join(c['npcId'] for c in configs)}")
    if args.out:
        _write_json(args.out, configs)
    if args.scenarios_out:
        _write_json(args.scenarios_out, scenarios)
    return 0


def _write_json(path: Path, data: object):
    # 셸의 > 대신 직접 쓴다: Windows PowerShell 5.1은 > 로 저장하면 BOM을 붙여 JSON 읽기가 깨진다
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"저장: {path}")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows 터미널에서도 한글이 깨지지 않게
    sys.stderr.reconfigure(encoding="utf-8")
    sys.exit(main())
