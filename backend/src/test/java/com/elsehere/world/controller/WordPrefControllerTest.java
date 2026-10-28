package com.elsehere.world.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.dto.WordPrefResponse;
import com.elsehere.world.service.WordPrefService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 저장 규칙은 WordPrefServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = WordPrefController.class)
class WordPrefControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private WordPrefService wordPrefService;

  @Test
  @DisplayName("PUT /api/v1/me/word-prefs/{wordKey}는 로그인한 사용자와 본문 값으로 처리하고 설정 결과를 돌려준다")
  void changeHoverHidden_respondsPref() throws Exception {
    given(wordPrefService.changeHoverHidden(1L, "sofa", true))
        .willReturn(new WordPrefResponse("sofa", true));

    mockMvc
        .perform(prefRequest("sofa", "{\"hoverHidden\": true}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.wordKey").value("sofa"))
        .andExpect(jsonPath("$.hoverHidden").value(true));
  }

  @Test
  @DisplayName("요청 본문에 hoverHidden이 없으면 400 VALIDATION_FAILED로 응답한다")
  void changeHoverHiddenWithoutValue_isRejected() throws Exception {
    mockMvc
        .perform(prefRequest("sofa", "{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("없는 단어 키면 404 NOT_FOUND 오류 응답을 돌려준다")
  void changeHoverHidden_unknownWord_respondsNotFound() throws Exception {
    given(wordPrefService.changeHoverHidden(1L, "moon_rock", true))
        .willThrow(new BusinessException(ErrorCode.NOT_FOUND));

    mockMvc
        .perform(prefRequest("moon_rock", "{\"hoverHidden\": true}"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/me/word-prefs/sofa")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"hoverHidden\": true}"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 없으면 403 CSRF_HEADER_REQUIRED로 거절한다")
  void withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/me/word-prefs/sofa")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"hoverHidden\": true}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  private static MockHttpServletRequestBuilder prefRequest(String wordKey, String body) {
    return put("/api/v1/me/word-prefs/" + wordKey)
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
