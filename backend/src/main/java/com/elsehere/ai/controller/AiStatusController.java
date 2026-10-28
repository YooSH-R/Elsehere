package com.elsehere.ai.controller;

import com.elsehere.ai.dto.AiStatusResponse;
import com.elsehere.ai.service.AiStatusService;
import com.elsehere.common.auth.LoginUser;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

/** AI 이용 상태와 기능별 가능 여부(AI-01). */
@RestController
@RequiredArgsConstructor
public class AiStatusController {

  private final AiStatusService aiStatusService;

  @GetMapping("/api/v1/ai/status")
  public AiStatusResponse status(@AuthenticationPrincipal LoginUser loginUser) {
    return aiStatusService.status(loginUser.userId());
  }
}
