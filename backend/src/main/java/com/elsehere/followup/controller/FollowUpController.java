package com.elsehere.followup.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.followup.dto.FollowUpListResponse;
import com.elsehere.followup.service.FollowUpService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
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

  /** CONV-14 학습자가 기다림 카드를 닫는다. 그 NPC는 다시 부르지 않는다. */
  @PostMapping("/api/v1/follow-ups/{followUpId}/dismiss")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void dismiss(@AuthenticationPrincipal LoginUser loginUser, @PathVariable Long followUpId) {
    followUpService.dismiss(loginUser.userId(), followUpId);
  }
}
