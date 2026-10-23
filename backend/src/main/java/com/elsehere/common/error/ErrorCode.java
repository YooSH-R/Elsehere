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
  INTERNAL_ERROR(HttpStatus.INTERNAL_SERVER_ERROR, "일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.");

  private final HttpStatus status;
  private final String message;
}
