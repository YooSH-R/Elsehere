package com.elsehere.common.error;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import java.util.Map;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** 공통 오류 응답을 검증하기 위해 예외를 던지는 테스트 전용 컨트롤러. */
@RestController
@RequestMapping("/test/errors")
class ErrorTestController {

  @GetMapping("/not-found")
  void throwNotFound() {
    throw new BusinessException(ErrorCode.NOT_FOUND);
  }

  @GetMapping("/with-details")
  void throwWithDetails() {
    throw new BusinessException(ErrorCode.NOT_FOUND, Map.of("targetId", 7));
  }

  @PostMapping("/validation")
  void validate(@Valid @RequestBody NameRequest request) {}

  @GetMapping("/unexpected")
  void throwUnexpected() {
    throw new IllegalStateException("DB 접속 정보 같은 내부 상세");
  }

  record NameRequest(@NotBlank String name) {}
}
