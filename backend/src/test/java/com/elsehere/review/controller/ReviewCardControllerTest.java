package com.elsehere.review.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.review.dto.ReviewCardListResponse;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewTarget;
import com.elsehere.review.service.ReviewCardService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = ReviewCardController.class)
class ReviewCardControllerTest {
  private static final ReviewCardResponse CARD =
      new ReviewCardResponse(
          42L,
          55L,
          "CAFE",
          2L,
          new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원"),
          "카페에서 포장을 부탁했어요.",
          List.of(
              new ReviewTarget("Can I get that to go?", "포장해 주세요.", null, null, null),
              new ReviewTarget("Could I get less ice?", "얼음을 줄여 주세요.", null, null, null)),
          0,
          null,
          List.of());

  @Autowired private MockMvc mockMvc;
  @MockitoBean private ReviewCardService reviewCardService;

  @Test
  void createUsesTheAuthenticatedOwnerAndReturnsTheStoredCard() throws Exception {
    given(reviewCardService.create(1L, 55L)).willReturn(CARD);

    mockMvc
        .perform(
            post("/api/v1/review-cards")
                .with(
                    authentication(
                        UsernamePasswordAuthenticationToken.authenticated(
                            new LoginUser(1L), null, List.of())))
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"conversationId\":55}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.id").value(42))
        .andExpect(jsonPath("$.sourceConversationId").value(55))
        .andExpect(jsonPath("$.npc.npcCode").value("CAFE_STAFF"))
        .andExpect(jsonPath("$.targets[0].expression").value("Can I get that to go?"));
  }

  @Test
  void ownerCanListReadAndDeleteCardsThroughTheApi() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(1L), null, List.of());
    given(reviewCardService.list(1L, "CAFE", null, 20))
        .willReturn(new ReviewCardListResponse(List.of(CARD), null));
    given(reviewCardService.detail(1L, 42L)).willReturn(CARD);

    mockMvc
        .perform(get("/api/v1/review-cards?sceneCode=CAFE").with(authentication(auth)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items[0].id").value(42))
        .andExpect(jsonPath("$.nextCursor").isEmpty());
    mockMvc
        .perform(get("/api/v1/review-cards/42").with(authentication(auth)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.id").value(42));
    mockMvc
        .perform(
            delete("/api/v1/review-cards/42")
                .with(authentication(auth))
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient"))
        .andExpect(status().isNoContent());
  }

  @Test
  void unauthenticatedReadRequiresLogin() throws Exception {
    mockMvc
        .perform(get("/api/v1/review-cards"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  void creatingWithoutTheChangeRequestHeaderIsForbidden() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/review-cards")
                .with(
                    authentication(
                        UsernamePasswordAuthenticationToken.authenticated(
                            new LoginUser(1L), null, List.of())))
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"conversationId\":55}"))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  @ParameterizedTest
  @ValueSource(strings = {"{}", "{\"conversationId\":0}", "{\"conversationId\":-1}"})
  void sourceConversationMustBeAPositiveId(String body) throws Exception {
    mockMvc
        .perform(
            post("/api/v1/review-cards")
                .with(
                    authentication(
                        UsernamePasswordAuthenticationToken.authenticated(
                            new LoginUser(1L), null, List.of())))
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content(body))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }
}
