package com.elsehere.common.error;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.Map;

/** 공통 오류 응답 본문. {@code { "error": { "code", "message", "details" } }} 형식이다. */
public record ErrorResponse(Error error) {

  public static ErrorResponse of(ErrorCode errorCode, Map<String, Object> details) {
    return of(errorCode, errorCode.getMessage(), details);
  }

  /** 오류 코드의 기본 문구 대신 상황에 맞는 문구를 보낼 때 쓴다. */
  public static ErrorResponse of(ErrorCode errorCode, String message, Map<String, Object> details) {
    return new ErrorResponse(new Error(errorCode.name(), message, details));
  }

  /** {@link ErrorCode}에 없는 요청 오류(405, 415 등)에 쓴다. */
  public static ErrorResponse of(String code, String message) {
    return new ErrorResponse(new Error(code, message, null));
  }

  /** {@code details}는 있을 때만 응답에 넣는다. */
  @JsonInclude(JsonInclude.Include.NON_NULL)
  public record Error(String code, String message, Map<String, Object> details) {}
}
