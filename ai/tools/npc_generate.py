"""입력(npcId·MBTI·역할·장소·교대)과 재료 파일(ai/npcs/rules/persona-ingredients.json)로 NPC 인격 카드를 만드는 명령.
조합 규칙은 AI 워커의 app.npc_persona에 있고(워커 API와 함께 쓴다), 여기서는 파일을 읽고 쓴다. 설계는 docs/npc-life-design.md.

    ai/.venv/Scripts/python ai/tools/npc_generate.py <npcId> <MBTI> <역할> <장소> <교대> --out <폴더> [--narrate]

<폴더>/<npcId>.json(인격 설정)과 <폴더>/life/<npcId>.json(생활 파일)을 쓴다. 생성 결과는 초안이라 isActive false다.
생활 파일의 근무지는 장소이고 교대는 공통 규칙(ai/npcs/rules/world-rules.json)의 그 장소 교대 가운데 하나다.
--narrate를 주면 LLM이 뼈대를 읽고 사연(배경 이야기·겪은 일)만 쓴다(ai/worker/.env의 구성, NPC 대답 모델).
나머지 칸은 규칙으로 고른 그대로다. 서비스 NPC로 쓰려면 사람이 보고 ai/npcs로 옮긴 뒤 목소리를 정해 켠다.
"""

import argparse
import asyncio
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))  # 인격 생성 규칙은 같은 저장소 AI 워커에 있다

# side·generate·narrate는 테스트가 npc_generate에서 가져다 쓰는 이름이기도 하다
from app.llm import LLM
from app.npc_persona import generate, load_behaviors, load_ingredients, narrate, side, with_candidates  # noqa: F401

NPCS = Path(__file__).resolve().parents[2] / "ai/npcs"


async def _narrate_with_settings(config: dict) -> dict:
    """ai/worker/.env의 구성으로 LLM을 열어 사연을 쓴다. 터미널 대화 도구처럼 NPC 대답 공급자 키만 있으면 된다."""
    from npc_chat import fill_unused_provider_keys

    from app import config as worker_config
    from app.config import Settings

    worker_config.load_dotenv()
    os.environ.setdefault("AI_WORKER_TOKEN", "npc-generate")  # 서버로 띄우지 않으므로 Spring과 공유할 토큰은 쓰지 않는다
    fill_unused_provider_keys(os.environ.get("LLM_PROFILE") or worker_config.DEFAULT_PROFILE)
    llm = LLM(Settings.from_env())
    try:
        return await narrate(config, llm)
    finally:
        await llm.aclose()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="입력과 재료로 NPC 인격 카드 뼈대를 만들어 인격 설정·생활 파일로 저장한다")
    parser.add_argument("npc_id", help="새 NPC의 id. 씨앗으로도 쓴다 (예: GEN_CAFE_01)")
    parser.add_argument("mbti", help="예: INTJ-T")
    parser.add_argument("role", choices=["STAFF", "PASSERBY"])
    parser.add_argument("place", help="기본 장소이자 근무지 (예: CAFE, HOTEL, TOWN)")
    parser.add_argument("shift", help="근무지에서 맡을 교대. 공통 규칙 places의 그 장소 교대 이름 (예: OPENING)")
    parser.add_argument("--out", type=Path, required=True, help="저장할 폴더. <npcId>.json과 life/<npcId>.json을 쓴다")
    parser.add_argument("--narrate", action="store_true", help="LLM이 사연(배경 이야기·겪은 일)을 쓴다. 없으면 사연은 비워 둔다")
    args = parser.parse_args(argv)
    # 일과는 근무지의 교대에서 계산하므로, 공통 규칙에 없는 장소나 교대로는 생활 파일을 쓰지 않는다
    places = json.loads((NPCS / "rules/world-rules.json").read_text(encoding="utf-8"))["places"]
    if args.place not in places:
        parser.error(f"공통 규칙 places에 없는 장소 '{args.place}'")
    if args.shift not in places[args.place]["shifts"]:
        parser.error(f"{args.place}의 교대는 {', '.join(places[args.place]['shifts'])} 중 하나")

    taken = {json.loads(path.read_text(encoding="utf-8"))["npcProfile"]["name"] for path in NPCS.glob("*.json")}
    config, life = generate(args.npc_id, args.role, args.place, load_ingredients(), load_behaviors(), mbti=args.mbti, taken_names=taken)
    if args.narrate:
        config = asyncio.run(_narrate_with_settings(config))
    (args.out / "life").mkdir(parents=True, exist_ok=True)
    _write_json(args.out / f"{args.npc_id}.json", config)
    _write_json(args.out / "life" / f"{args.npc_id}.json", {**life, "workplace": args.place, "shift": args.shift})
    return 0


def _write_json(path: Path, data: object):
    # 셸의 > 대신 직접 쓴다: Windows PowerShell 5.1은 > 로 저장하면 BOM을 붙여 JSON 읽기가 깨진다
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"저장: {path}")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows 터미널에서도 한글이 깨지지 않게
    sys.exit(main())
