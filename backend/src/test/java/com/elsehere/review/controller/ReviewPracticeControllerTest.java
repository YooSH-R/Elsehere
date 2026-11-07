package com.elsehere.review.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.review.dto.ReviewPracticeProgressResponse;
import com.elsehere.review.dto.ReviewPracticeResultResponse;
import com.elsehere.review.service.ReviewPracticeResults;
import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = ReviewPracticeController.class)
class ReviewPracticeControllerTest {
  @Autowired private MockMvc mvc;
  @MockitoBean private ReviewPracticeResults results;

  @Test
  void progressRequiresAuthentication() throws Exception {
    mvc.perform(get("/api/v1/review-cards/42/conversations/77/progress"))
        .andExpect(status().isUnauthorized());
  }

  @Test
  void progressRouteUsesTheAuthenticatedOwnerWithoutEndingPractice() throws Exception {
    given(results.progress(1L, 42L, 77L))
        .willReturn(new ReviewPracticeProgressResponse(77L, 42L, 1, List.of()));
    var auth =
        authentication(
            UsernamePasswordAuthenticationToken.authenticated(new LoginUser(1L), null, List.of()));
    mvc.perform(get("/api/v1/review-cards/42/conversations/77/progress").with(auth))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.conversationId").value(77))
        .andExpect(jsonPath("$.verifiedExpressionCount").value(1))
        .andExpect(jsonPath("$.completedAt").doesNotExist())
        .andExpect(jsonPath("$.practiceCount").doesNotExist());
  }

  @Test
  void missingOrOtherOwnersResultReturnsNotFound() throws Exception {
    given(results.latest(1L, 42L)).willThrow(new BusinessException(ErrorCode.NOT_FOUND));
    var auth =
        authentication(
            UsernamePasswordAuthenticationToken.authenticated(new LoginUser(1L), null, List.of()));
    mvc.perform(get("/api/v1/review-cards/42/result").with(auth))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  void resultsRequireAuthentication() throws Exception {
    mvc.perform(get("/api/v1/review-cards/42/result")).andExpect(status().isUnauthorized());
  }

  @Test
  void routesReturnTheAuthenticatedOwnersSavedResults() throws Exception {
    var result =
        new ReviewPracticeResultResponse(
            77L, 42L, false, 1, 1, Instant.parse("2026-11-06T00:00:00Z"), List.of(), List.of());
    given(results.latest(1L, 42L)).willReturn(result);
    given(results.detail(1L, 42L, 77L)).willReturn(result);
    var auth =
        authentication(
            UsernamePasswordAuthenticationToken.authenticated(new LoginUser(1L), null, List.of()));
    mvc.perform(get("/api/v1/review-cards/42/result").with(auth))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.conversationId").value(77))
        .andExpect(jsonPath("$.practiceCompleted").value(false));
    mvc.perform(get("/api/v1/review-cards/42/conversations/77/result").with(auth))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.reviewCardId").value(42));
  }
}
