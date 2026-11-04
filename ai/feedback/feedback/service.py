"""독립 실행 피드백 처리. 재시도·DB·사용량 차감·Redis 처리는 호출 측 책임."""

import json

from pydantic import ValidationError

from .clients import ModelClient, ProviderCallError
from .models import INPUT, LLM_OUTPUT, usable_turns, validate_pair
from .prompt import build_messages
from .situation_catalog import keep_useful
from .spoken_usage import build_spoken_notes


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("중복 JSON 키")
        result[key] = value
    return result


def _reject_constant(value):
    raise ValueError("비표준 JSON 상수")


def _base(request):
    return {
        name: getattr(request, name) for name in ("schema_version", "request_id", "conversation_revision", "analysis_config_version")
    } | {"scope": request.scope.model_dump()}


def _response(request, *, status, result=None, problem=None):
    response = _base(request) | {"status": status, "result": result, "problem": problem}
    _, checked = validate_pair(request, response)
    return checked.model_dump(mode="json")


def _failed(request, code, message):
    return _response(
        request,
        status="failed",
        problem={
            # 이미 승인된 한 번의 호출과 새 재시도 예산 승인은 다르다.
            "code": code,
            "message_ko": message,
            "retryable_now": False,
        },
    )


def generate_feedback(payload, client: ModelClient, *, ai_allowed: bool):
    """요청 -> 프롬프트 -> 모델 -> 구조/근거 검사 -> 결과.

    ai_allowed는 서버가 결정한 새 호출 승인 결과다. 이 함수는 예산을 예약하지 않는다.
    입력 오류는 ValidationError로 호출자에게 전달한다. 누락된 GMS 설정도 명시적 예외다.
    """
    if not isinstance(ai_allowed, bool):
        raise TypeError("ai_allowed에는 서버가 확인한 bool 값을 전달해야 한다")
    request = INPUT.validate_python(payload)
    if not usable_turns(request):
        return _response(
            request,
            status="insufficient_evidence",
            problem={
                "code": "NO_USABLE_USER_TURNS",
                "message_ko": "평가할 수 있는 확정 사용자 발화가 없어요.",
            },
        )
    if not ai_allowed:
        return _response(
            request,
            status="blocked_by_quota",
            problem={
                "code": "AI_QUOTA_EXHAUSTED",
                "message_ko": "AI 이용량이 부족해 새 피드백을 만들지 못했어요.",
                "retryable_now": False,
            },
        )
    messages = build_messages(request)
    try:
        raw = client.complete(messages)
    except TimeoutError:
        return _failed(request, "AI_TIMEOUT", "피드백 생성 시간이 초과됐어요.")
    except ProviderCallError:
        return _failed(request, "AI_PROVIDER_ERROR", "피드백 서비스에 일시적인 문제가 있어요.")
    try:
        if not isinstance(raw, str):
            raise ValueError("모델 어댑터 반환값은 JSON 문자열이어야 한다")
        body = json.loads(raw, object_pairs_hook=_unique_object, parse_constant=_reject_constant)
        checked = LLM_OUTPUT.validate_python(body)
        if checked.outcome == "insufficient_evidence":
            return _response(request, status="insufficient_evidence", problem=checked.problem.model_dump())
        result = checked.result.model_dump()
        user_texts = [turn.final_text for turn in request.turns if turn.speaker == "user" and turn.text_status == "final"]
        result["situation_expressions"] = keep_useful(result["situation_expressions"], user_texts)
        result["spoken_usage_notes"] = build_spoken_notes(request, checked.result, checked.spoken_usage_selection)
        return _response(request, status="completed", result=result)
    except (ValueError, TypeError, ValidationError):
        # 모델 원문·공급자 오류 상세를 사용자에게 출력하거나 로그에 남기지 않는다.
        return _failed(request, "INVALID_MODEL_OUTPUT", "피드백 결과를 확인하지 못했어요.")
