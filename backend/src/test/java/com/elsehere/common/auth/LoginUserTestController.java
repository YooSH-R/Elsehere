package com.elsehere.common.auth;

import java.util.Map;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** 인증과 현재 사용자 주입을 검증하기 위한 테스트 전용 컨트롤러. 받은 사용자 ID를 그대로 돌려준다. */
@RestController
@RequestMapping("/api/v1/test/me")
public class LoginUserTestController {

  @GetMapping
  Map<String, Long> read(@AuthenticationPrincipal LoginUser loginUser) {
    return Map.of("userId", loginUser.userId());
  }

  @PostMapping
  Map<String, Long> change(@AuthenticationPrincipal LoginUser loginUser) {
    return Map.of("userId", loginUser.userId());
  }
}
