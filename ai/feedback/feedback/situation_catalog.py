"""실제 장소와 학습자 발화에 맞는 참고 표현 한 개를 찾고, 추천 결과를 정리한다."""

import json
import re
from functools import lru_cache
from pathlib import Path

CATALOG_PATH = Path(__file__).resolve().parent.parent / "content" / "situation_expressions.ko.json"
NPC_ONLY = {"you're all set", "you are all set", "what can i get you", "anything else", "how can i help you"}
HANGUL = re.compile(r"[가-힣]")


@lru_cache(maxsize=1)
def _items():
    return json.loads(CATALOG_PATH.read_text(encoding="utf-8"))["items"]


def situation_candidate(scene, user_texts):
    """장소가 같고 학습자가 관련 말을 실제로 했을 때만 고정 표현을 참고로 제공한다."""
    spoken = [_norm(text) for text in user_texts]
    place_items = [item for item in _items() if scene in item["scenes"]]
    best = None
    best_hits = 0
    for item in keep_useful(place_items, user_texts):
        hits = sum(any(_matches_keyword(keyword, text) for text in spoken) for keyword in item["keywords"])
        if hits > best_hits:
            best = item
            best_hits = hits
    if best is None:
        return None
    return {key: best[key] for key in ("expression", "meaning_ko", "usage_ko", "example_en", "example_ko")}


def _matches_keyword(keyword, text):
    keyword = _norm(keyword)
    if len(keyword) < 2:
        return False
    if HANGUL.search(keyword):
        return keyword in text
    return f" {keyword} " in f" {text} "


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
