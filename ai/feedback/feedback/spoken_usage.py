"""검토한 회화 콘텐츠 후보 검색과 서버측 설명 연결. 네트워크 호출 없음."""
import json
from pathlib import Path
import re

CATALOG_PATH = Path(__file__).resolve().parents[1] / "content/spoken_usage.ko.json"


def load_catalog():
    return json.loads(CATALOG_PATH.read_text(encoding="utf-8"))


def spoken_candidates(request):
    catalog = load_catalog()
    candidates = []
    for turn in request.turns:
        if turn.speaker != "user" or turn.text_status != "final":
            continue
        text = turn.final_text.replace("’", "'")
        for entry in catalog:
            match = None
            for pattern in entry["patterns"]:
                match = re.search(pattern, text, re.IGNORECASE)
                if match:
                    break
            if entry["catalog_id"] == "C06" and match:
                prefix = text[max(0, match.start() - 40):match.start()]
                if re.search(r"\b(?:not|never)\b|n't\b", prefix, re.IGNORECASE):
                    continue
            if match:
                start = max(0, match.start() - 60)
                quote = turn.final_text[start:start + 160]
                candidates.append({
                    "catalog_id": entry["catalog_id"], "expression": entry["expression"],
                    "meaning_ko": entry["meaning_ko"], "caution_ko": entry["usage_ko"],
                    "usage_mode": "understanding_only" if entry["priority"] == "이해용" else "context_dependent",
                    "source": {"turn_id": turn.turn_id, "quote": quote},
                })
    return candidates[:3]


def build_spoken_notes(request, result, selection):
    if selection is None:
        return []
    for candidate in spoken_candidates(request):
        source = {"turn_id": selection.turn_id, "quote": selection.quote}
        if candidate["catalog_id"] != selection.catalog_id or candidate["source"] != source:
            continue
        review = next((item for item in result.turn_reviews if item.turn_id == selection.turn_id), None)
        if review is None or review.verdict in ("needs_clarification", "not_assessable"):
            return []
        entry = next(item for item in load_catalog() if item["catalog_id"] == selection.catalog_id)
        return [{key: entry[key] for key in (
            "catalog_id", "expression", "meaning_ko", "usage_ko", "example_en", "example_ko", "sources",
        )} | {"source": source, "example_kind": "learning_example"}]
    # 잘못된 선택은 선택적 설명만 생략한다. 교정 결과를 버리거나 재호출하지 않는다.
    return []
