package com.elsehere.admin.controller;

import com.elsehere.admin.dto.AdminLoginRequest;
import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminLoginService;
import com.elsehere.admin.service.AdminLoginSession;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 어드민 로그인·로그아웃. 로그인 화면(/admin/login)의 스크립트가 부른다. 게임 로그인과는 따로다(docs/admin.md). */
@RestController
@RequiredArgsConstructor
public class AdminLoginController {

  private final AdminLoginService adminLoginService;
  private final AdminLoginSession adminLoginSession;

  @PostMapping("/api/v1/admin/login")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void login(
      @Valid @RequestBody AdminLoginRequest body,
      HttpServletRequest request,
      HttpServletResponse response) {
    AdminLogin adminLogin = adminLoginService.login(body.loginId(), body.password());
    adminLoginSession.login(adminLogin, request, response);
  }

  /** 어드민 로그인만 끝낸다. 같은 브라우저의 게임 로그인은 그대로 둔다. */
  @PostMapping("/api/v1/admin/logout")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void logout(HttpServletRequest request, HttpServletResponse response) {
    adminLoginSession.logout(request, response);
  }
}
