package com.elsehere.feedback.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.feedback.dto.ConversationFeedbackResponse;
import com.elsehere.feedback.dto.DetailedFeedbackResponse;
import com.elsehere.feedback.dto.FeedbackRequest;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.service.FeedbackService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 피드백 다시 보기(FB-01)·다시 만들기(FB-02)·상세 평가(FB-03). 종료 때 생성은 CONV-08이 한다. */
@RestController
@RequiredArgsConstructor
public class FeedbackController {

  private final FeedbackService feedbackService;

  @GetMapping("/api/v1/conversations/{conversationId}/feedback")
  public ConversationFeedbackResponse feedback(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long conversationId) {
    return feedbackService.feedback(loginUser.userId(), conversationId);
  }

  @PostMapping("/api/v1/conversations/{conversationId}/feedback")
  public FeedbackResponse regenerate(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody FeedbackRequest request) {
    return feedbackService.regenerate(loginUser.userId(), conversationId, request.requestId());
  }

  @PostMapping("/api/v1/conversations/{conversationId}/detailed-feedback")
  public DetailedFeedbackResponse detailed(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody FeedbackRequest request) {
    return feedbackService.detailed(loginUser.userId(), conversationId);
  }
}
