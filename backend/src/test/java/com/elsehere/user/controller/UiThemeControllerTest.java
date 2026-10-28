package com.elsehere.user.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.dto.UiThemeResponse;
import com.elsehere.user.entity.UiTheme;
import com.elsehere.user.service.UiThemeService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 저장 규칙은 UiThemeServiceTest에서 DB와 함께 검증하고, 여기서는 요청 검증과 응답 형식만 본다.
@SecuredWebMvcTest(controllers = UiThemeController.class)
class UiThemeControllerTest {

  private static final String PATH = "/api/v1/me/ui-theme";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private UiThemeService uiThemeService;

  @Test
  @DisplayName("로그인한 사용자가 테마를 보내면 그 사용자의 테마로 저장하고 저장한 테마를 돌려준다")
  void changeUiTheme_respondsChosenTheme() throws Exception {
    given(uiThemeService.changeUiTheme(1L, UiTheme.STICKER))
        .willReturn(new UiThemeResponse(UiTheme.STICKER));

    mockMvc
        .perform(uiThemeRequest("{\"uiTheme\": \"STICKER\"}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.uiTheme").value("STICKER"));
  }

  @ParameterizedTest
  @ValueSource(
      strings = {
        "{}",
        "{\"uiTheme\": null}",
        "{\"uiTheme\": \"\"}",
        "{\"uiTheme\": \"NEON\"}",
        "{\"uiTheme\": \"sticker\"}"
      })
  @DisplayName("테마가 없거나 JELLY·STICKER·CLOUD가 아니면 저장하지 않고 400 VALIDATION_FAILED로 응답한다")
  void missingOrUnknownTheme_isRejected(String body) throws Exception {
    mockMvc
        .perform(uiThemeRequest(body))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(uiThemeService).should(never()).changeUiTheme(anyLong(), any());
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            put(PATH)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"uiTheme\": \"STICKER\"}"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 없으면 403 CSRF_HEADER_REQUIRED로 거절한다")
  void withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(
            put(PATH)
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"uiTheme\": \"STICKER\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  private static MockHttpServletRequestBuilder uiThemeRequest(String body) {
    return put(PATH)
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
