package com.elsehere.common.error;

import lombok.Getter;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;

/** API 오류 코드. 코드와 상태는 MVP API 명세의 오류 응답 표를 따른다. */
@Getter
@RequiredArgsConstructor
public enum ErrorCode {
  VALIDATION_FAILED(HttpStatus.BAD_REQUEST, "요청 값이 올바르지 않습니다."),
  UNAUTHENTICATED(HttpStatus.UNAUTHORIZED, "로그인이 필요합니다."),
  SIGNUP_REQUIRED(HttpStatus.FORBIDDEN, "가입 정보를 먼저 입력해 주세요."),
  CSRF_HEADER_REQUIRED(HttpStatus.FORBIDDEN, "요청 확인 헤더가 없습니다."),
  NOT_FOUND(HttpStatus.NOT_FOUND, "대상을 찾을 수 없습니다."),
  ALREADY_SIGNED_UP(HttpStatus.CONFLICT, "이미 가입한 계정입니다."),
  NICKNAME_TAKEN(HttpStatus.CONFLICT, "이미 사용 중인 닉네임입니다."),
  LEVEL_TEST_NOT_IN_PROGRESS(HttpStatus.CONFLICT, "이미 끝난 레벨 테스트입니다."),
  SCENE_CLOSED(HttpStatus.CONFLICT, "아직 열리지 않은 장소입니다."),
  TUTORIAL_NOT_IN_PROGRESS(HttpStatus.CONFLICT, "튜토리얼 단계가 아닙니다."),
  MISSION_NOT_READY(HttpStatus.CONFLICT, "주문을 마치고 카페에서 대화를 끝낸 뒤 쉬어 가세요."),
  REQUEST_ID_CONFLICT(HttpStatus.CONFLICT, "같은 요청 ID로 다른 요청을 보냈습니다."),
  CONVERSATION_ALREADY_ACTIVE(HttpStatus.CONFLICT, "진행 중인 대화가 있습니다."),
  CONVERSATION_NOT_ACTIVE(HttpStatus.CONFLICT, "이미 끝난 대화입니다."),
  AUDIO_TOO_LARGE(HttpStatus.CONTENT_TOO_LARGE, "녹음이 너무 깁니다. 짧게 나눠 말해 주세요."),
  AUDIO_FORMAT_UNSUPPORTED(HttpStatus.UNSUPPORTED_MEDIA_TYPE, "지원하지 않는 녹음 형식입니다."),
  STT_NO_SPEECH(HttpStatus.UNPROCESSABLE_CONTENT, "말소리를 알아듣지 못했어요. 다시 말해 주세요."),
  VOCAB_SOURCE_MISMATCH(HttpStatus.UNPROCESSABLE_CONTENT, "저장할 수 없는 단어입니다."),
  AI_RATE_LIMITED(HttpStatus.TOO_MANY_REQUESTS, "요청이 많아 잠시 후 다시 시도해 주세요."),
  AI_RESPONSE_INVALID(HttpStatus.BAD_GATEWAY, "AI 응답을 처리하지 못했습니다. 다시 시도해 주세요."),
  AI_TEMPORARILY_UNAVAILABLE(HttpStatus.SERVICE_UNAVAILABLE, "AI가 잠시 응답하지 않습니다. 다시 시도해 주세요."),
  INTERNAL_ERROR(HttpStatus.INTERNAL_SERVER_ERROR, "일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.");

  private final HttpStatus status;
  private final String message;
}
