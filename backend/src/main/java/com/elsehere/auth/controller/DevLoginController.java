package com.elsehere.auth.controller;

import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import lombok.RequiredArgsConstructor;
import org.springframework.context.annotation.Profile;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * 소셜 로그인 없이 개발할 수 있게 해 주는 개발용 로그인. local 프로필에서만 등록되며 다른 환경에는 이 API가 없다.
 *
 * <p>사용자가 실제로 있는지는 확인하지 않고 요청한 ID로 로그인 세션을 만든다.
 */
@Profile("local")
@RestController
@RequestMapping("/api/v1/dev")
@RequiredArgsConstructor
public class DevLoginController {

  private final LoginSessionService loginSessionService;

  @PostMapping("/login")
  public ResponseEntity<Void> login(
      @Valid @RequestBody DevLoginRequest request,
      HttpServletRequest httpRequest,
      HttpServletResponse httpResponse) {
    loginSessionService.login(new LoginUser(request.userId()), httpRequest, httpResponse);
    return ResponseEntity.noContent().build();
  }

  public record DevLoginRequest(@NotNull @Positive Long userId) {}
}
