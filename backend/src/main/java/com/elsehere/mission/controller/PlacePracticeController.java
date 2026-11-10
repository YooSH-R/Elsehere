package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.PracticePlaceListResponse;
import com.elsehere.mission.dto.PracticeProgressResponse;
import com.elsehere.mission.service.PlacePracticeService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RestController;

/** 장소 연습 목록(PRC-01)과 연습 대화 진행(PRC-03). 연습 대화 시작(PRC-02)은 대화 시작 흐름이라 ConversationController가 받는다. */
@RestController
@RequiredArgsConstructor
public class PlacePracticeController {
  private final PlacePracticeService service;

  @GetMapping("/api/v1/practice-places")
  public PracticePlaceListResponse places(@AuthenticationPrincipal LoginUser user) {
    return service.places(user.userId());
  }

  @GetMapping("/api/v1/practice-scenarios/{scenarioId}/conversations/{conversationId}/progress")
  public PracticeProgressResponse progress(
      @AuthenticationPrincipal LoginUser user,
      @PathVariable String scenarioId,
      @PathVariable Long conversationId) {
    return service.progress(user.userId(), scenarioId, conversationId);
  }
}
