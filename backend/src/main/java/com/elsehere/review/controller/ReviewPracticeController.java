package com.elsehere.review.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.review.dto.ReviewPracticeProgressResponse;
import com.elsehere.review.dto.ReviewPracticeResultResponse;
import com.elsehere.review.service.ReviewPracticeResults;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
public class ReviewPracticeController {
  private final ReviewPracticeResults results;

  @GetMapping("/api/v1/review-cards/{cardId}/conversations/{conversationId}/progress")
  public ReviewPracticeProgressResponse progress(
      @AuthenticationPrincipal LoginUser user,
      @PathVariable Long cardId,
      @PathVariable Long conversationId) {
    return results.progress(user.userId(), cardId, conversationId);
  }

  @GetMapping("/api/v1/review-cards/{cardId}/result")
  public ReviewPracticeResultResponse latest(
      @AuthenticationPrincipal LoginUser user, @PathVariable Long cardId) {
    return results.latest(user.userId(), cardId);
  }

  @GetMapping("/api/v1/review-cards/{cardId}/conversations/{conversationId}/result")
  public ReviewPracticeResultResponse detail(
      @AuthenticationPrincipal LoginUser user,
      @PathVariable Long cardId,
      @PathVariable Long conversationId) {
    return results.detail(user.userId(), cardId, conversationId);
  }
}
