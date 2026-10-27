package com.elsehere.feedback.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.feedback.dto.ConversationFeedbackResponse;
import com.elsehere.feedback.dto.DetailedFeedbackItemResponse;
import com.elsehere.feedback.dto.DetailedFeedbackResponse;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.dto.IdiomResponse;
import com.elsehere.feedback.dto.ReviewsResponse;
import com.elsehere.feedback.dto.SuggestionResponse;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.service.FeedbackService;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 피드백 생성·저장 규칙은 FeedbackServiceTest에서 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = FeedbackController.class)
class FeedbackControllerTest {

  private static final UUID REQUEST_ID = UUID.fromString("00000000-0000-0000-0000-000000000001");
  private static final FeedbackResponse DONE =
      new FeedbackResponse(
          FeedbackStatus.DONE,
          "라테를 주문했어요.",
          new ReviewsResponse(List.of("분명히 말했어요"), List.of("공손하게 말해 보세요")),
          new SuggestionResponse("I want latte", "I'd like a latte, please."),
          null);
  private static final DetailedFeedbackItemResponse ITEM =
      new DetailedFeedbackItemResponse(
          1L,
          11L,
          "I want latte",
          "I'd like a latte, please.",
          "더 공손해요.",
          new IdiomResponse("I'd like ~", "~로 주세요"),
          false);

  @Autowired private MockMvc mockMvc;

  @MockitoBean private FeedbackService feedbackService;

  @Test
  @DisplayName("GET /feedback은 짧은 피드백과 상세 평가를 함께 돌려준다")
  void feedback_respondsFeedbackAndDetails() throws Exception {
    given(feedbackService.feedback(1L, 5L))
        .willReturn(
            new ConversationFeedbackResponse(
                5L,
                new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원"),
                "CAFE",
                1,
                DONE,
                List.of(),
                List.of(ITEM)));

    mockMvc
        .perform(get("/api/v1/conversations/5/feedback").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.feedback.status").value("DONE"))
        .andExpect(jsonPath("$.feedback.reviews.good[0]").value("분명히 말했어요"))
        .andExpect(jsonPath("$.feedback.suggestion.betterText").value("I'd like a latte, please."))
        .andExpect(jsonPath("$.detailedFeedback[0].idiom.meaningKo").value("~로 주세요"));
  }

  @Test
  @DisplayName("POST /feedback은 다시 만든 피드백을 돌려준다")
  void regenerate_respondsFeedback() throws Exception {
    given(feedbackService.regenerate(1L, 5L, REQUEST_ID)).willReturn(DONE);

    mockMvc
        .perform(request(post("/api/v1/conversations/5/feedback"), requestIdBody()))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("DONE"))
        .andExpect(jsonPath("$.summary").value("라테를 주문했어요."));
  }

  @Test
  @DisplayName("POST /detailed-feedback은 발화별 평가 목록을 돌려준다")
  void detailed_respondsItems() throws Exception {
    given(feedbackService.detailed(1L, 5L))
        .willReturn(new DetailedFeedbackResponse(5L, List.of(ITEM)));

    mockMvc
        .perform(request(post("/api/v1/conversations/5/detailed-feedback"), requestIdBody()))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items[0].turnId").value(11))
        .andExpect(jsonPath("$.items[0].userText").value("I want latte"))
        .andExpect(jsonPath("$.items[0].hasModelSpeech").value(false));
  }

  @Test
  @DisplayName("requestId가 없으면 400 VALIDATION_FAILED, 없는 대화면 404 NOT_FOUND다")
  void invalidRequestOrUnknownConversation_isRejected() throws Exception {
    mockMvc
        .perform(request(post("/api/v1/conversations/5/feedback"), "{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    given(feedbackService.feedback(1L, 99L)).willThrow(new BusinessException(ErrorCode.NOT_FOUND));
    mockMvc
        .perform(get("/api/v1/conversations/99/feedback").with(authentication(loggedIn(1L))))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  private static String requestIdBody() {
    return "{\"requestId\": \"" + REQUEST_ID + "\"}";
  }

  private static MockHttpServletRequestBuilder request(
      MockHttpServletRequestBuilder builder, String body) {
    return builder
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content(body)
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
