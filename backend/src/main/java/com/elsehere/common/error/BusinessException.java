package com.elsehere.common.error;

import java.util.Map;
import lombok.Getter;

/** 오류 코드로 응답할 업무 예외. 공통 오류 응답으로 바뀌어 클라이언트에 전달된다. */
@Getter
public class BusinessException extends RuntimeException {

  private final ErrorCode errorCode;
  private final Map<String, Object> details;

  public BusinessException(ErrorCode errorCode) {
    this(errorCode, null, null);
  }

  /** 클라이언트가 화면 처리에 쓸 정보를 {@code details}에 담아 보낸다. */
  public BusinessException(ErrorCode errorCode, Map<String, Object> details) {
    this(errorCode, details, null);
  }

  /** 다른 예외를 업무 오류로 바꿀 때 원인을 함께 남긴다. */
  public BusinessException(ErrorCode errorCode, Throwable cause) {
    this(errorCode, null, cause);
  }

  private BusinessException(ErrorCode errorCode, Map<String, Object> details, Throwable cause) {
    super(errorCode.getMessage(), cause);
    this.errorCode = errorCode;
    this.details = details;
  }
}
