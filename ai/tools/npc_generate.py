"""입력(npcId·MBTI·역할·장소)과 재료 파일(ai/npcs/rules/persona-ingredients.json)로 NPC 인격 카드의 뼈대를 만든다.
LLM 없이 규칙으로만 고르고, npcId를 씨앗으로 써서 같은 입력이면 언제나 같은 NPC가 나온다. 설계는 docs/npc-life-design.md.

    ai/.venv/Scripts/python ai/tools/npc_generate.py <npcId> <MBTI> <역할> <장소> --out <폴더>

<폴더>/<npcId>.json(인격 설정)과 <폴더>/life/<npcId>.json(생활 파일)을 쓴다. 생성 결과는 초안이라 isActive false이고,
배경 이야기(사연)는 비어 있다. 서비스 NPC로 쓰려면 사람이 보고 ai/npcs로 옮긴 뒤 목소리를 정해 켠다.
"""

import argparse
import json
import random
import sys
from pathlib import Path

import npc_derive

NPCS = Path(__file__).resolve().parents[2] / "ai/npcs"
VALUES_PER_NPC, MOTIVATIONS_PER_NPC, INTERESTS_PER_NPC = 2, 2, 3


def side(value: float) -> str:
    """성격 축 값이 어느 쪽인지: 0.5보다 크면 high, 작으면 low, 같으면 middle."""
    return "high" if value > 0.5 else "low" if value < 0.5 else "middle"


def generate(npc_id: str, mbti: str, role: str, place: str, ingredients: dict, library: dict, taken_names=frozenset()) -> tuple[dict, dict]:
    """(인격 설정, 생활 파일). taken_names는 다른 NPC가 이미 쓰는 이름이라 고르지 않는다."""
    dimensions = npc_derive.mbti_to_dimensions(mbti)
    rng = random.Random(npc_id)
    name = rng.choice([name for name in ingredients["names"] if name not in taken_names])
    occupation = rng.choice(_occupations(ingredients, role, place))
    profile = {
        "schemaVersion": "2.0",
        "profileVersion": 1,
        "name": name,
        "occupation": occupation["text"],
        "ageRange": rng.choice(occupation.get("ages", ingredients["ageRanges"])),
        "personality": {"traits": _traits(ingredients["traits"], dimensions, rng)},
        "values": _texts(ingredients["values"], dimensions, role, rng, VALUES_PER_NPC),
        "motivations": _texts(ingredients["motivations"], dimensions, role, rng, MOTIVATIONS_PER_NPC),
        "socialBehavior": _social_behavior(ingredients["socialBehavior"], dimensions, role),
        "speechStyle": _speech_style(ingredients["speechStyle"], dimensions, role),
        "background": {
            "summary": None,
            "experiences": [],
            "interests": _texts(ingredients["interests"], dimensions, role, rng, INTERESTS_PER_NPC),
        },
        "behaviorRules": [],
    }
    life = {"schemaVersion": "1.0", "npcId": npc_id, "mbti": mbti, "role": role}
    profile = npc_derive.with_derived_traits(profile, life, library)
    config = {"npcId": npc_id, "npcProfile": profile, "defaultLocationId": place, "voiceId": None, "isActive": False}  # 목소리 미연결 초안
    return config, life


def _occupations(ingredients: dict, role: str, place: str) -> list[dict]:
    """역할에 맞는 직업. 직원은 일하는 장소까지 맞아야 한다."""
    fitting = [job for job in ingredients["occupations"] if job["role"] == role and job.get("place", place) == place]
    if not fitting:
        raise ValueError(f"{role} 역할로 {place}에서 할 직업이 재료에 없음")
    return fitting


def _traits(traits: list[dict], dimensions: dict[str, float], rng: random.Random) -> list[dict]:
    """성격 축마다 NPC가 기운 쪽의 특징 하나. 가운데(0.5)인 축은 넣지 않는다."""
    picked = []
    for axis in npc_derive.AXES:
        leaning = side(dimensions[axis])
        if leaning != "middle":
            trait = rng.choice([trait for trait in traits if trait["leans"] == {axis: leaning}])
            picked.append({"name": trait["name"], "description": trait["description"]})
    return picked


def _texts(items: list[dict], dimensions: dict[str, float], role: str, rng: random.Random, count: int) -> list[str]:
    """이 역할이 쓸 수 있고 성격 쪽이 맞거나 표시가 없는 재료에서 count개. 잘 맞을수록 잘 뽑히고(표시 없음은 0.5) 같은 것은 한 번만."""
    pool = []
    for item in items:
        fit = npc_derive.lean_fit(item["leans"], dimensions) if "leans" in item else 0.5
        if role in item.get("roles", [role]) and (fit > 0.5 or "leans" not in item):
            pool.append((item["text"], fit))
    picked = []
    while pool and len(picked) < count:
        text = rng.choices([text for text, _ in pool], weights=[fit for _, fit in pool])[0]
        picked.append(text)
        pool = [(other, fit) for other, fit in pool if other != text]
    return picked


def _by_side(rule: dict, dimensions: dict[str, float]) -> str:
    return rule[side(dimensions[rule["axis"]])]


def _social_behavior(rules: dict, dimensions: dict[str, float], role: str) -> dict:
    return {
        "firstMeeting": _by_side(rules["firstMeeting"], dimensions),
        "familiarityExpression": _by_side(rules["familiarityExpression"], dimensions),
        "conflictResponse": _by_side(rules["conflictResponse"], dimensions),
        "boundaries": list(rules["boundaries"][role]),
    }


def _speech_style(rules: dict, dimensions: dict[str, float], role: str) -> dict:
    return {
        "tone": " and ".join(_by_side(rule, dimensions) for rule in rules["tone"]),
        "formality": rules["formality"][role],
        "verbosity": _by_side(rules["verbosity"], dimensions),
        "directness": _by_side(rules["directness"], dimensions),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="입력과 재료로 NPC 인격 카드 뼈대를 만들어 인격 설정·생활 파일로 저장한다 (LLM 없음)")
    parser.add_argument("npc_id", help="새 NPC의 id. 씨앗으로도 쓴다 (예: GEN_CAFE_01)")
    parser.add_argument("mbti", help="예: INTJ-T")
    parser.add_argument("role", choices=["STAFF", "PASSERBY"])
    parser.add_argument("place", help="기본 장소 (예: CAFE, HOTEL, TOWN)")
    parser.add_argument("--out", type=Path, required=True, help="저장할 폴더. <npcId>.json과 life/<npcId>.json을 쓴다")
    args = parser.parse_args(argv)

    ingredients = json.loads((NPCS / "rules/persona-ingredients.json").read_text(encoding="utf-8"))
    library = json.loads((NPCS / "rules/behaviors.json").read_text(encoding="utf-8"))
    taken = {json.loads(path.read_text(encoding="utf-8"))["npcProfile"]["name"] for path in NPCS.glob("*.json")}
    config, life = generate(args.npc_id, args.mbti, args.role, args.place, ingredients, library, taken_names=taken)
    (args.out / "life").mkdir(parents=True, exist_ok=True)
    _write_json(args.out / f"{args.npc_id}.json", config)
    _write_json(args.out / "life" / f"{args.npc_id}.json", life)
    return 0


def _write_json(path: Path, data: object):
    # 셸의 > 대신 직접 쓴다: Windows PowerShell 5.1은 > 로 저장하면 BOM을 붙여 JSON 읽기가 깨진다
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"저장: {path}")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows 터미널에서도 한글이 깨지지 않게
    sys.exit(main())
