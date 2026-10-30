package com.elsehere.admin.controller;

import com.elsehere.admin.dto.AdminGrantRequest;
import com.elsehere.admin.dto.AdminUser;
import com.elsehere.admin.service.AdminUserService;
import com.elsehere.common.auth.LoginUser;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 운영자 어드민의 관리자 지정·해제 API. 관리자 권한은 보안 설정(SecurityConfig)이 확인한다. */
@RestController
@RequiredArgsConstructor
public class AdminUserController {

  private final AdminUserService adminUserService;

  /** Google 이메일로 관리자를 추가한다. 이미 관리자여도 같은 결과를 돌려준다. */
  @PostMapping("/api/v1/admin/admins")
  public AdminUser grant(
      @AuthenticationPrincipal LoginUser loginUser, @Valid @RequestBody AdminGrantRequest request) {
    return adminUserService.grant(loginUser.userId(), request.email());
  }

  /** 관리자를 해제한다. 자기 자신은 해제할 수 없다. */
  @DeleteMapping("/api/v1/admin/admins/{userId}")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void revoke(@AuthenticationPrincipal LoginUser loginUser, @PathVariable Long userId) {
    adminUserService.revoke(loginUser.userId(), userId);
  }
}
