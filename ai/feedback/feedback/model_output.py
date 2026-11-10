"""모델 전용 축약 응답을 공개 피드백 계약으로 복원한다. 평가·근거 검증은 기존 검증기가 맡는다."""

import difflib
import re
from copy import deepcopy

from .models import contains_korean, surface_only_change, usable_turns
from .situation_catalog import keep_useful

# 설명이 인용한 말. “…”, "…", ‘…’ (닫는 ’ 뒤에 글자가 오면 I’ve 같은 축약의 아포스트로피라 인용 끝이 아니다)
_QUOTED = re.compile(r"“([^”]+)”|\"([^\"]+)\"|‘(.+?)’(?![A-Za-z])")

COMPACT_VERSION = "compact-v1"
SERVER_FIELDS = {
    "TurnReview": ("review_id", "original_text", "assisted"),
    "Suggestion": ("suggestion_id",),
    "ReviewTag": ("tag_id", "assisted"),
    "VocabCandidate": ("candidate_id",),
}


def _objects(value):
    if not isinstance(value, list) or any(not isinstance(item, dict) for item in value):
        raise ValueError("모델 결과에는 객체 배열이 필요하다")
    return value


def _server_fields(item, kind, values):
    if any(name in item for name in SERVER_FIELDS[kind]):
        raise ValueError("축약 응답에 서버 전용 필드가 포함됐다")
    item.update(values)


def _drop_voice_surface_suggestions(result, users):
    """음성 원문에 대소문자·끝 문장부호만 바꾼 교정은 계약상 교정이 아니다(validate_pair가 거부한다).
    그 교정 하나 때문에 피드백 전체를 버리지 않도록 교정만 빼고, 남은 제안이 없으면 개선 제안 판정을 적절로 둔다.
    반환: 교정을 뺀 사용자 turn_id (제안 ID와 같다)."""
    removed = set()
    for review in result["turn_reviews"]:
        turn = users[review["turn_id"]]
        if turn.input_type != "voice":
            continue
        suggestions = review["suggestions"]
        kept = [
            item
            for item in suggestions
            if not (
                isinstance(item.get("suggested_expression"), str) and surface_only_change(turn.final_text, item["suggested_expression"])
            )
        ]
        if len(kept) == len(suggestions):
            continue
        removed.add(review["turn_id"])
        review["suggestions"] = kept
        if not kept and review.get("verdict") == "improvement_suggested":
            review["verdict"] = "appropriate"
    if result.get("summary_suggestion_id") in removed:
        remaining = [review["turn_id"] for review in result["turn_reviews"] if review["suggestions"]]
        result["summary_suggestion_id"] = remaining[0] if remaining else None
    return removed


def _plain(text):
    """인용 비교용: 소문자, 둥근 따옴표를 곧은 따옴표로, 문장부호를 빼고 공백 하나로."""
    text = text.lower().replace("’", "'").replace("‘", "'")
    return " ".join(re.sub(r"[^a-z0-9' ]+", " ", text).split())


def _names_missing_words(explanation, original, suggested):
    """설명이 원문에도 고친 문장에도 없는 영어를 인용하는지. 그런 설명은 화면의 '이렇게 말해요'와 다른 교정을 말한다.
    한국어 인용(뜻 풀이)과 ~·… 이 든 형태 설명("want to ~")은 문장 비교 대상이 아니다."""
    words = (_plain(original), _plain(suggested))
    for match in _QUOTED.finditer(explanation):
        quote = next(group for group in match.groups() if group is not None)
        if contains_korean(quote) or any(mark in quote for mark in ("~", "…", "...")) or not re.search(r"[A-Za-z]", quote):
            continue
        if not any(f" {_plain(quote)} " in f" {text} " for text in words):
            return True
    return False


def _change_note(original, suggested):
    """원문과 고친 문장의 실제 차이를 앞뒤 한 단어와 함께 보여 주는 설명. 모델 설명이 교정과 어긋날 때만 쓴다."""
    before, after = original.split(), suggested.split()
    plain_before = [_plain(word) for word in before]
    plain_after = [_plain(word) for word in after]
    changes = [op for op in difflib.SequenceMatcher(a=plain_before, b=plain_after, autojunk=False).get_opcodes() if op[0] != "equal"]
    if not changes:
        return f"“{original.strip()}” 대신 “{suggested.strip()}”라고 하면 자연스러워요."
    start_a, start_b = max(changes[0][1] - 1, 0), max(changes[0][3] - 1, 0)
    end_a, end_b = min(changes[-1][2] + 1, len(before)), min(changes[-1][4] + 1, len(after))
    return f"“{' '.join(before[start_a:end_a])}” 대신 “{' '.join(after[start_b:end_b])}”라고 하면 자연스러워요."


def _align_explanations(result, users):
    """'이 표현을 쓰는 이유'가 '이렇게 말해요'에 없는 교정을 말하지 않게 한다(S15P31A305-780).
    모델이 설명과 다르게 문장을 고치면, 어긋난 설명을 맞는 쪽 설명이나 실제 차이를 짚는 설명으로 바꾼다."""
    for review in result["turn_reviews"]:
        if not review["suggestions"]:
            continue
        suggestion = review["suggestions"][0]
        original, suggested = users[review["turn_id"]].final_text, suggestion.get("suggested_expression")
        if not isinstance(suggested, str):
            continue
        fits = {
            field: isinstance(owner.get("explanation_ko"), str) and not _names_missing_words(owner["explanation_ko"], original, suggested)
            for field, owner in (("review", review), ("suggestion", suggestion))
        }
        if all(fits.values()):
            continue
        if fits["suggestion"]:
            replacement = suggestion["explanation_ko"]
        elif fits["review"]:
            replacement = review["explanation_ko"]
        else:
            replacement = _change_note(original, suggested)
        for field, owner in (("review", review), ("suggestion", suggestion)):
            if not fits[field]:
                owner["explanation_ko"] = replacement


def _vocab_source_text(source, users, reviews):
    """단어장 후보 출처의 실제 글: 사용자 원문 또는 연결된 개선 표현. 확인할 수 없으면 None (검증기가 거부한다)."""
    if source.get("type") == "user_turn":
        turn = users.get(source.get("turn_id"))
        return None if turn is None else turn.final_text
    if source.get("type") == "suggestion":
        review = reviews.get(source.get("review_id"))
        suggestions = [] if review is None else review["suggestions"]
        return next(
            (item.get("suggested_expression") for item in suggestions if item.get("suggestion_id") == source.get("suggestion_id")), None
        )
    return None


def _usable_vocab(candidates, users, turn_reviews, removed):
    """단어장 후보는 선택 항목이라, 표현이 출처 글에 없는 후보 하나 때문에 피드백 전체를 버리지 않는다.
    표현이 출처 글에 그대로 있으면 quote를 그 표현으로 맞추고, 없으면 그 후보만 뺀다. 출처 자체가 없으면 검증기가 거부한다."""
    reviews = {review["turn_id"]: review for review in turn_reviews}
    kept = []
    for candidate in candidates:
        source = candidate.get("source")
        if isinstance(source, dict) and source.get("type") == "suggestion" and source.get("suggestion_id") in removed:
            continue
        expression = candidate.get("expression")
        text = _vocab_source_text(source, users, reviews) if isinstance(source, dict) else None
        if isinstance(expression, str) and isinstance(text, str) and expression != source.get("quote"):
            if not expression.strip() or expression not in text:
                continue
            source["quote"] = expression
        kept.append(candidate)
    return kept


def restore_model_output(request, body):
    # 기존 고정 응답·샘플·저장 계약은 별도 경로로 그대로 검증한다. 누락된 필드를 추측해서 채우지 않는다.
    if not isinstance(body, dict) or "format_version" not in body:
        return body
    if body["format_version"] != COMPACT_VERSION:
        raise ValueError("지원하지 않는 모델 출력 버전")
    body = deepcopy(body)
    del body["format_version"]
    result = body.get("result")
    if not isinstance(result, dict):
        return body
    users = {turn.turn_id: turn for turn in usable_turns(request)}
    for review in _objects(result.get("turn_reviews")):
        turn_id = review.get("turn_id")
        if not isinstance(turn_id, str) or turn_id not in users:
            raise ValueError("이번 대화의 확정 사용자 발화만 평가할 수 있다")
        turn = users[turn_id]
        _server_fields(
            review, "TurnReview", {"review_id": turn_id, "original_text": turn.final_text, "assisted": bool(turn.assistance_ids)}
        )
        for suggestion in _objects(review.get("suggestions")):
            # 생성 스키마는 발화당 제안 하나만 허용한다. 참조는 실제 사용자 turn_id로 통일한다.
            _server_fields(suggestion, "Suggestion", {"suggestion_id": turn_id})
    removed = _drop_voice_surface_suggestions(result, users)
    _align_explanations(result, users)
    for index, tag in enumerate(_objects(result.get("review_tags")), 1):
        evidence = _objects(tag.get("evidence"))
        linked = [item.get("turn_id") for item in evidence]
        if any(not isinstance(turn_id, str) or turn_id not in users for turn_id in linked):
            raise ValueError("리뷰 태그에는 확정 사용자 발화 근거가 필요하다")
        _server_fields(tag, "ReviewTag", {"tag_id": f"tag{index}", "assisted": any(users[turn_id].assistance_ids for turn_id in linked)})
    for candidate in _objects(result.get("vocab_candidates")):
        _server_fields(candidate, "VocabCandidate", {})
    result["vocab_candidates"] = _usable_vocab(result["vocab_candidates"], users, result["turn_reviews"], removed)
    for index, candidate in enumerate(result["vocab_candidates"], 1):
        candidate["candidate_id"] = f"vocab{index}"
    situations = _objects(result.get("situation_expressions", []))
    for item in situations:
        if item.get("speaker_role", "learner") not in ("learner", "npc", "both"):
            raise ValueError("표현의 화자 역할을 확인하지 못했다")
    result["situation_expressions"] = keep_useful(situations, [])
    for item in result["situation_expressions"]:
        item.pop("speaker_role", None)
    return body
