package com.elsehere.admin.controller;

import com.elsehere.admin.service.AdminLogin;
import java.util.Map;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

/** 어드민 보안 설정을 검증하기 위한 테스트 전용 컨트롤러. 어드민 화면·관리 API 주소에서 받은 어드민 아이디를 그대로 돌려준다. */
@RestController
public class AdminLoginTestController {

  @GetMapping("/admin/test")
  Map<String, String> page(@AuthenticationPrincipal AdminLogin adminLogin) {
    return Map.of("loginId", adminLogin.loginId());
  }

  @GetMapping("/api/v1/admin/test/me")
  Map<String, String> read(@AuthenticationPrincipal AdminLogin adminLogin) {
    return Map.of("loginId", adminLogin.loginId());
  }
}
