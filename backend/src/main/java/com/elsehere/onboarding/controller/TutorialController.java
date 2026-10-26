package com.elsehere.onboarding.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.onboarding.dto.TutorialResponse;
import com.elsehere.onboarding.service.TutorialService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RestController;

/** 튜토리얼 완료(TUT-01)와 건너뛰기(TUT-02). */
@RestController
@RequiredArgsConstructor
public class TutorialController {

  private final TutorialService tutorialService;

  @PostMapping("/api/v1/me/tutorial/complete")
  public TutorialResponse complete(@AuthenticationPrincipal LoginUser loginUser) {
    return tutorialService.complete(loginUser.userId());
  }

  @PostMapping("/api/v1/me/tutorial/skip")
  public TutorialResponse skip(@AuthenticationPrincipal LoginUser loginUser) {
    return tutorialService.skip(loginUser.userId());
  }
}
