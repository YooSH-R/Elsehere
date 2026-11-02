package com.elsehere.dialogue.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.dialogue.dto.HintResponse;
import com.elsehere.dialogue.dto.MeaningResponse;
import com.elsehere.dialogue.dto.SuggestWordsResponse;
import com.elsehere.dialogue.service.TurnHelpService;
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

// 저장·재사용·소유권 규칙은 TurnHelpServiceTest에서 검증하고, 여기서는 요청 검증과 응답 형식만 본다.
@SecuredWebMvcTest(controllers = TurnHelpController.class)
class TurnHelpControllerTest {

  private static final UUID REQUEST_ID = UUID.fromString("00000000-0000-0000-0000-000000000001");

  @Autowired private MockMvc mockMvc;

  @MockitoBean private TurnHelpService turnHelpService;

  @Test
  @DisplayName("POST /api/v1/turns/{turnId}/meaning은 대사 뜻과 단어 풀이를 돌려준다")
  void meaning_respondsMeaningAndGlossary() throws Exception {
    given(turnHelpService.meaning(1L, 12L, REQUEST_ID))
        .willReturn(
            new MeaningResponse(
                12L, "좋아요! 매장에서 드실 건가요, 가져가실 건가요?", List.of(new GlossaryResponse("to go", "포장"))));

    mockMvc
        .perform(withRequestId(post("/api/v1/turns/12/meaning")))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.turnId").value(12))
        .andExpect(jsonPath("$.meaningKo").value("좋아요! 매장에서 드실 건가요, 가져가실 건가요?"))
        .andExpect(jsonPath("$.glossary[0].text").value("to go"))
        .andExpect(jsonPath("$.glossary[0].meaningKo").value("포장"));
  }

  @Test
  @DisplayName("POST /api/v1/turns/{turnId}/hint는 추천 문장을 돌려준다")
  void hint_respondsHintText() throws Exception {
    given(turnHelpService.hint(1L, 12L, REQUEST_ID))
        .willReturn(new HintResponse(12L, "For here, please."));

    mockMvc
        .perform(withRequestId(post("/api/v1/turns/12/hint")))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.turnId").value(12))
        .andExpect(jsonPath("$.hintText").value("For here, please."));
  }

  @Test
  @DisplayName("POST /api/v1/turns/{turnId}/suggest-words는 추천 단어를 돌려준다")
  void suggestWords_respondsWords() throws Exception {
    given(turnHelpService.suggestWords(1L, 12L, REQUEST_ID))
        .willReturn(new SuggestWordsResponse(12L, List.of("for here", "to go")));

    mockMvc
        .perform(withRequestId(post("/api/v1/turns/12/suggest-words")))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.turnId").value(12))
        .andExpect(jsonPath("$.suggestWords[0]").value("for here"))
        .andExpect(jsonPath("$.suggestWords[1]").value("to go"));
  }

  @Test
  @DisplayName("뜻 보기·힌트·추천 단어 요청에 requestId가 없으면 400 VALIDATION_FAILED다")
  void help_withoutRequestId_isRejected() throws Exception {
    for (String path :
        List.of(
            "/api/v1/turns/12/meaning",
            "/api/v1/turns/12/hint",
            "/api/v1/turns/12/suggest-words")) {
      mockMvc
          .perform(
              post(path)
                  .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                  .contentType(MediaType.APPLICATION_JSON)
                  .content("{}")
                  .with(authentication(loggedIn(1L))))
          .andExpect(status().isBadRequest())
          .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    }
  }

  private static MockHttpServletRequestBuilder withRequestId(
      MockHttpServletRequestBuilder builder) {
    return builder
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content("{\"requestId\": \"" + REQUEST_ID + "\"}")
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
