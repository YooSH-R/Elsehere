package com.elsehere.user.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.dto.UiThemeRequest;
import com.elsehere.user.dto.UiThemeResponse;
import com.elsehere.user.service.UiThemeService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 집에서 고른 화면 테마를 계정에 저장한다(USER-01). */
@RestController
@RequiredArgsConstructor
public class UiThemeController {

  private final UiThemeService uiThemeService;

  @PutMapping("/api/v1/me/ui-theme")
  public UiThemeResponse changeUiTheme(
      @AuthenticationPrincipal LoginUser loginUser, @Valid @RequestBody UiThemeRequest request) {
    return uiThemeService.changeUiTheme(loginUser.userId(), request.uiTheme());
  }
}
