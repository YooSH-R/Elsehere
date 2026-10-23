package com.elsehere.bootstrap.controller;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.bootstrap.service.BootstrapService;
import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.bind.annotation.SessionAttribute;

/**
 * 접속 직후 한 번 호출해 첫 화면을 정한다(BOOT-01).
 *
 * <p>가입 대기 사용자도 호출할 수 있어야 하므로 보안 설정에서 경로를 열어 두고, 로그인 상태는 여기서 구분한다.
 */
@RestController
@RequiredArgsConstructor
public class BootstrapController {

  private final BootstrapService bootstrapService;
  private final LoginSessionService loginSessionService;

  @GetMapping("/api/v1/bootstrap")
  public BootstrapResponse bootstrap(
      @AuthenticationPrincipal LoginUser loginUser,
      @SessionAttribute(name = PendingSignup.SESSION_ATTRIBUTE, required = false)
          PendingSignup pendingSignup,
      HttpServletRequest request,
      HttpServletResponse response) {
    if (loginUser == null) {
      if (pendingSignup != null) {
        return BootstrapResponse.signupRequired();
      }
      throw new BusinessException(ErrorCode.UNAUTHENTICATED);
    }

    return bootstrapService
        .findSignedIn(loginUser.userId())
        .orElseThrow(
            () -> {
              // 탈퇴했거나 없는 ID로 개발용 로그인한 세션이다. 다시 로그인하도록 로그인 상태를 비운다.
              loginSessionService.clearLogin(request, response);
              return new BusinessException(ErrorCode.UNAUTHENTICATED);
            });
  }
}
