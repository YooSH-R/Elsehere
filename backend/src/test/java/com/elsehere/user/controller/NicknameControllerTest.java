package com.elsehere.user.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.service.NicknameService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

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
