"""입력(npcId·MBTI·역할·장소)과 재료 파일(ai/npcs/rules/persona-ingredients.json)로 NPC 인격 카드의 뼈대를 만든다.
LLM 없이 규칙으로만 고르고, npcId를 씨앗으로 써서 같은 입력이면 언제나 같은 NPC가 나온다. 설계는 docs/npc-life-design.md.

    ai/.venv/Scripts/python ai/tools/npc_generate.py <npcId> <MBTI> <역할> <장소> --out <폴더> [--narrate]

<폴더>/<npcId>.json(인격 설정)과 <폴더>/life/<npcId>.json(생활 파일)을 쓴다. 생성 결과는 초안이라 isActive false다.
--narrate를 주면 LLM이 뼈대를 읽고 사연(배경 이야기·겪은 일)만 쓴다(ai/worker/.env의 구성, NPC 대답 모델).
나머지 칸은 규칙으로 고른 그대로다. 서비스 NPC로 쓰려면 사람이 보고 ai/npcs로 옮긴 뒤 목소리를 정해 켠다.
"""

import argparse
import asyncio
import json
import os
import random
import sys
from pathlib import Path

import npc_configs  # 공통 인격 스키마 검사. 같은 저장소 AI 워커(app)를 가져올 경로도 함께 연다
import npc_derive

from app.llm import LLM, Usage

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


NARRATE_SYSTEM = """You write the backstory for a character in an English conversation game set in a small American town.
You get the character's card as JSON. Write only:
- "summary": two or three plain sentences about who they are and how they came to be this way.
- "experiences": one or two past events, each with "event" (what happened, one sentence) and "influence" (how it shapes the way they act now, one sentence).
Tie the story to the traits, values, motivations, interests and behavior rules on the card so they read as one person, and give a reason behind at least one of them.
Never change or contradict the card. Do not name real people or brands, and do not mention any places, shops, streets or services in the town: the game decides those.
Keep it ordinary and warm-hearted: no tragedy, crime, illness or stereotypes."""

NARRATE_SCHEMA = {
    "type": "object",
    "properties": {
        "summary": {"type": "string"},
        "experiences": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"event": {"type": "string"}, "influence": {"type": "string"}},
                "required": ["event", "influence"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["summary", "experiences"],
    "additionalProperties": False,
}


async def narrate(config: dict, llm: LLM) -> dict:
    """규칙 뼈대의 사연 칸(background.summary·experiences)만 LLM이 쓴 새 설정. 다른 칸은 그대로다.
    결과가 공통 인격 스키마를 지키지 않으면 ValueError (저장하지 않는다)."""
    profile = config["npcProfile"]
    card = {key: value for key, value in profile.items() if key not in ("schemaVersion", "profileVersion")}
    card["background"] = {"interests": profile["background"]["interests"]}  # 비어 있는 사연 칸은 보여 주지 않는다
    messages = [{"role": "system", "content": NARRATE_SYSTEM}, {"role": "user", "content": json.dumps(card, ensure_ascii=False)}]
    out = await llm.chat("narrate", "npc", messages, Usage(), temperature=0.8, max_tokens=500, schema=NARRATE_SCHEMA, hedge=False)
    try:
        story = json.loads(out)
    except json.JSONDecodeError as e:
        raise ValueError(f"모델 출력이 JSON이 아님: {out[:80]!r}") from e
    background = {**profile["background"], "summary": story["summary"], "experiences": story["experiences"]}
    narrated = {**config, "npcProfile": {**profile, "background": background}}
    errors = npc_configs.schema_errors(narrated)
    if errors:
        raise ValueError("사연이 공통 인격 스키마에 맞지 않음: " + "; ".join(f"{where}: {why}" for where, why in errors))
    return narrated


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
    parser.add_argument("place", help="기본 장소 (예: CAFE, HOTEL, TOWN)")
    parser.add_argument("--out", type=Path, required=True, help="저장할 폴더. <npcId>.json과 life/<npcId>.json을 쓴다")
    parser.add_argument("--narrate", action="store_true", help="LLM이 사연(배경 이야기·겪은 일)을 쓴다. 없으면 사연은 비워 둔다")
    args = parser.parse_args(argv)

    ingredients = json.loads((NPCS / "rules/persona-ingredients.json").read_text(encoding="utf-8"))
    library = json.loads((NPCS / "rules/behaviors.json").read_text(encoding="utf-8"))
    taken = {json.loads(path.read_text(encoding="utf-8"))["npcProfile"]["name"] for path in NPCS.glob("*.json")}
    config, life = generate(args.npc_id, args.mbti, args.role, args.place, ingredients, library, taken_names=taken)
    if args.narrate:
        config = asyncio.run(_narrate_with_settings(config))
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
