"""장면별로 검토한 '이 상황에서 자주 써요' 후보. 모델이 매번 새로 지어내지 않고 이 후보에서 고르게 한다."""

import json
import re
from functools import lru_cache
from pathlib import Path

CATALOG_PATH = Path(__file__).resolve().parent.parent / "content" / "situation_expressions.ko.json"
MAX_CANDIDATES = 8
MIN_EXPRESSIONS = 2
NPC_ONLY = {"you're all set", "you are all set", "what can i get you", "anything else", "how can i help you"}
HANGUL = re.compile(r"[가-힣]")


@lru_cache(maxsize=1)
def _items():
    return json.loads(CATALOG_PATH.read_text(encoding="utf-8"))["items"]


def situation_candidates(request):
    """장면(scene_id)의 후보 가운데 scene_description에 걸리는 말이 많은 것부터 최대 8개. 숙어(idiom)를 앞에 둔다."""
    scene = request.context.scene_id
    description = request.context.scene_description or ""
    scored = []
    for order, item in enumerate(_items()):
        if scene not in item["scenes"]:
            continue
        if _norm(item["expression"]).replace("'", "") in {value.replace("'", "") for value in NPC_ONLY}:
            continue
        hits = sum(1 for word in item["keywords"] if word in description)
        scored.append((-hits, item["kind"] != "idiom", order, item))
    scored.sort(key=lambda entry: entry[:3])
    return [
        {key: item[key] for key in ("id", "kind", "expression", "meaning_ko", "usage_ko", "example_en", "example_ko")}
        for *_, item in scored[:MAX_CANDIDATES]
    ]


def fill_up(kept, candidates, user_texts):
    """거르고 남은 표현이 2개가 안 되면 검토된 장면 후보에서 사용자가 말하지 않은 것으로 2개까지 채운다.

    모델이 고른 표현이 직원 전용이거나 이미 말한 것이어서 빠지면 복습할 표현이 모자라게 된다. 후보가 없는 장면은 그대로 둔다.
    """
    filled = list(kept)
    chosen = {_norm(item["expression"]) for item in filled}
    for candidate in keep_useful(candidates, user_texts):
        if len(filled) >= MIN_EXPRESSIONS:
            break
        if _norm(candidate["expression"]) in chosen:
            continue
        filled.append({key: value for key, value in candidate.items() if key not in ("id", "kind")})
        chosen.add(_norm(candidate["expression"]))
    return filled


def _norm(text):
    return " ".join(re.sub(r"[^\w\s']", " ", text).split()).casefold()


def keep_useful(situations, user_texts):
    """사용자가 이미 말한 표현, 영어 칸에 한글이 있거나 뜻 칸에 한글이 없는 항목은 뺀다. 전체 결과는 실패시키지 않는다."""
    spoken = [_norm(text) for text in user_texts]
    kept = []
    for item in situations:
        expression = _norm(item["expression"])
        if item.get("speaker_role") == "npc" or expression in NPC_ONLY:
            continue
        if HANGUL.search(item["expression"]) or not HANGUL.search(item["meaning_ko"]):
            continue
        if expression and any(expression in text for text in spoken):
            continue
        kept.append(item)
    return kept
