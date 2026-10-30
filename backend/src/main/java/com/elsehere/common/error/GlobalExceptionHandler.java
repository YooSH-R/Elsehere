package com.elsehere.common.error;

import java.util.Arrays;
import java.util.Map;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.MissingServletRequestParameterException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.method.annotation.HandlerMethodValidationException;
import org.springframework.web.multipart.support.MissingServletRequestPartException;
import org.springframework.web.servlet.NoHandlerFoundException;
import org.springframework.web.servlet.resource.NoResourceFoundException;

/** 컨트롤러에서 발생한 예외를 공통 오류 응답으로 바꾼다. */
@Slf4j
@RestControllerAdvice
public class GlobalExceptionHandler {

  private static final String REQUEST_REJECTED_MESSAGE = "처리할 수 없는 요청입니다.";

  @ExceptionHandler(BusinessException.class)
  public ResponseEntity<ErrorResponse> handleBusinessException(BusinessException exception) {
    ErrorCode errorCode = exception.getErrorCode();
    return ResponseEntity.status(errorCode.getStatus())
        .body(ErrorResponse.of(errorCode, exception.getMessage(), exception.getDetails()));
  }

  @ExceptionHandler({
    MethodArgumentNotValidException.class,
    HttpMessageNotReadableException.class,
    HandlerMethodValidationException.class,
    MissingServletRequestParameterException.class,
    MissingServletRequestPartException.class
  })
  public ResponseEntity<ErrorResponse> handleInvalidRequest(Exception exception) {
    return respond(ErrorCode.VALIDATION_FAILED, null);
  }

  @ExceptionHandler({NoResourceFoundException.class, NoHandlerFoundException.class})
  public ResponseEntity<ErrorResponse> handleUnknownPath(Exception exception) {
    return respond(ErrorCode.NOT_FOUND, null);
  }

  /**
   * 그 밖의 예외. 지원하지 않는 메서드·형식처럼 Spring MVC가 상태를 정한 요청 오류는 그 상태와 상태 이름을 코드로 돌려준다. 나머지는 서버 오류로 보고 원인은
   * 로그에만 남기며, 응답에는 내부 메시지를 담지 않는다.
   */
  @ExceptionHandler(Exception.class)
  public ResponseEntity<ErrorResponse> handleOther(Exception exception) {
    if (exception instanceof org.springframework.web.ErrorResponse requestError) {
      HttpStatusCode status = requestError.getStatusCode();
      HttpStatus httpStatus = HttpStatus.valueOf(status.value());
      return ResponseEntity.status(status)
          .body(ErrorResponse.of(httpStatus.name(), REQUEST_REJECTED_MESSAGE));
    }
    // 예외 메시지에는 외부 응답이나 사용자 입력이 섞일 수 있어 타입과 코드 위치만 기록한다.
    log.error(
        "처리하지 못한 예외 type={} stack={}",
        exception.getClass().getName(),
        Arrays.toString(exception.getStackTrace()));
    return respond(ErrorCode.INTERNAL_ERROR, null);
  }

  private ResponseEntity<ErrorResponse> respond(ErrorCode errorCode, Map<String, Object> details) {
    return ResponseEntity.status(errorCode.getStatus()).body(ErrorResponse.of(errorCode, details));
  }
}
