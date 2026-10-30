package com.elsehere.user.controller;

import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.NicknameChangeResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.service.NicknameService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 중복 여부는 NicknameServiceTest에서 DB와 함께 검증하고, 여기서는 호출 자격·요청 검증·응답 형식만 본다.
@SecuredWebMvcTest(controllers = NicknameController.class)
class NicknameControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private NicknameService nicknameService;

  @Test
  @DisplayName("가입 대기 중인 사용자는 닉네임 사용 가능 여부를 확인할 수 있다")
  void pendingSignup_checksAvailability() throws Exception {
    given(nicknameService.isAvailable("Sun")).willReturn(false);

    mockMvc
        .perform(
            get("/api/v1/nicknames/availability")
                .param("nickname", "Sun")
                .session(pendingSession()))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.nickname").value("Sun"))
        .andExpect(jsonPath("$.available").value(false));
  }

  @Test
  @DisplayName("로그인한 사용자도 닉네임 사용 가능 여부를 확인할 수 있다")
  void loggedInUser_checksAvailability() throws Exception {
    given(nicknameService.isAvailable("선종")).willReturn(true);

    mockMvc
        .perform(
            get("/api/v1/nicknames/availability")
                .param("nickname", "선종")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.nickname").value("선종"))
        .andExpect(jsonPath("$.available").value(true));
  }

  @Test
  @DisplayName("닉네임 규칙에 맞지 않으면 중복을 확인하지 않고 400 VALIDATION_FAILED로 응답한다")
  void nicknameBreakingRule_isRejected() throws Exception {
    mockMvc
        .perform(
            get("/api/v1/nicknames/availability")
                .param("nickname", "Sun!")
                .session(pendingSession()))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("닉네임을 보내지 않으면 400 VALIDATION_FAILED로 응답한다")
  void missingNickname_isRejected() throws Exception {
    mockMvc
        .perform(get("/api/v1/nicknames/availability").session(pendingSession()))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("가입 대기 중도 아니고 로그인하지도 않았으면 401 UNAUTHENTICATED로 거절한다")
  void anonymous_cannotCheckAvailability() throws Exception {
    mockMvc
        .perform(get("/api/v1/nicknames/availability").param("nickname", "Sun"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("로그인한 사용자가 닉네임을 보내면 그 사용자의 닉네임을 바꾸고 바꾼 닉네임을 돌려준다")
  void changeNickname_respondsNewNickname() throws Exception {
    given(nicknameService.changeNickname(1L, "선종")).willReturn(new NicknameChangeResponse("선종"));

    mockMvc
        .perform(nicknameChange("{\"nickname\": \"선종\"}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.nickname").value("선종"));
  }

  @ParameterizedTest
  @ValueSource(
      strings = {"{}", "{\"nickname\": null}", "{\"nickname\": \"S\"}", "{\"nickname\": \"Sun!\"}"})
  @DisplayName("닉네임이 없거나 규칙에 맞지 않으면 바꾸지 않고 400 VALIDATION_FAILED로 응답한다")
  void changeNickname_breakingRule_isRejected(String body) throws Exception {
    mockMvc
        .perform(nicknameChange(body))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(nicknameService).should(never()).changeNickname(anyLong(), anyString());
  }

  @Test
  @DisplayName("다른 사용자가 쓰는 닉네임이면 409 NICKNAME_TAKEN으로 응답한다")
  void changeNickname_takenNickname_respondsConflict() throws Exception {
    given(nicknameService.changeNickname(1L, "Moon"))
        .willThrow(new BusinessException(ErrorCode.NICKNAME_TAKEN));

    mockMvc
        .perform(nicknameChange("{\"nickname\": \"Moon\"}"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("NICKNAME_TAKEN"));
  }

  @Test
  @DisplayName("로그인하지 않으면 닉네임을 바꾸지 않고 401 UNAUTHENTICATED로 응답한다")
  void changeNickname_notLoggedIn_isRejected() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/me/nickname")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"nickname\": \"선종\"}"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static MockHttpServletRequestBuilder nicknameChange(String body) {
    return put("/api/v1/me/nickname")
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content(body)
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }

  private static MockHttpSession pendingSession() {
    MockHttpSession session = new MockHttpSession();
    session.setAttribute(
        PendingSignup.SESSION_ATTRIBUTE,
        new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com"));
    return session;
  }
}
