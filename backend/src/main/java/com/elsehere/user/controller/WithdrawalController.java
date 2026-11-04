package com.elsehere.user.controller;

import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.service.WithdrawalService;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.servlet.http.HttpSession;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 회원 탈퇴(USER-05). 마이페이지의 [회원 탈퇴]에서 부른다. */
@RestController
@RequiredArgsConstructor
public class WithdrawalController {

  private final WithdrawalService withdrawalService;
  private final LoginSessionService loginSessionService;

  /** 내용과 개인정보를 지우고 이 세션을 끝낸다. */
  @DeleteMapping("/api/v1/me")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void withdraw(
      @AuthenticationPrincipal LoginUser loginUser,
      HttpServletRequest request,
      HttpServletResponse response) {
    withdrawalService.withdraw(loginUser.userId());
    loginSessionService.clearLogin(request, response);
    HttpSession session = request.getSession(false);
    if (session != null) {
      session.invalidate();
    }
  }
}
