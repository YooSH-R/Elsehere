"""모델 전용 축약 응답을 공개 피드백 계약으로 복원한다. 평가·근거 검증은 기존 검증기가 맡는다."""

from copy import deepcopy

from .models import usable_turns

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
    for index, tag in enumerate(_objects(result.get("review_tags")), 1):
        evidence = _objects(tag.get("evidence"))
        linked = [item.get("turn_id") for item in evidence]
        if any(not isinstance(turn_id, str) or turn_id not in users for turn_id in linked):
            raise ValueError("리뷰 태그에는 확정 사용자 발화 근거가 필요하다")
        _server_fields(tag, "ReviewTag", {"tag_id": f"tag{index}", "assisted": any(users[turn_id].assistance_ids for turn_id in linked)})
    for index, candidate in enumerate(_objects(result.get("vocab_candidates")), 1):
        _server_fields(candidate, "VocabCandidate", {"candidate_id": f"vocab{index}"})
    return body
