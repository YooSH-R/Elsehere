package com.elsehere.followup.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.followup.dto.FollowUpListResponse;
import com.elsehere.followup.service.FollowUpService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
public class FollowUpController {

  private final FollowUpService followUpService;

  /** CONV-13 NPC가 곧 건네기로 했는데 아직 받지 않은 것. 새로고침·씬 이동 뒤 기다림 카드를 다시 그린다. */
  @GetMapping("/api/v1/follow-ups")
  public FollowUpListResponse waiting(@AuthenticationPrincipal LoginUser loginUser) {
    return new FollowUpListResponse(followUpService.waiting(loginUser.userId()));
  }
}
