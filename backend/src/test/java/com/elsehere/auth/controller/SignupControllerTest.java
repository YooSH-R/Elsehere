package com.elsehere.auth.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.hamcrest.Matchers.nullValue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.auth.service.ConvertingGuest;
import com.elsehere.auth.service.PendingSignup;
import com.elsehere.auth.service.SignupService;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.UiTheme;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 가입 규칙은 SignupServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답과 세션 처리만 본다.
@SecuredWebMvcTest(controllers = {SignupController.class, LoginUserTestController.class})
@Import(ConsentCatalog.class)
class SignupControllerTest {

  private static final UserResponse SIGNED_UP_USER =
      new UserResponse(
          1L, "Sun", AvatarType.F, null, OnboardingStep.LANG, UiTheme.JELLY, false, false);

  @Autowired private MockMvc mockMvc;

  @MockitoBean private SignupService signupService;

  @Test
  @DisplayName("가입 대기 상태면 가입 정보 입력에 필요한 이메일과 약관 목록을 돌려준다")
  void pendingSignup_getsSignupForm() throws Exception {
    mockMvc
        .perform(get("/api/v1/auth/signup").session(pendingSession()))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.email").value("sun@example.com"))
        .andExpect(jsonPath("$.consents.length()").value(6))
        .andExpect(jsonPath("$.consents[0].consentType").value("TERMS"))
        .andExpect(jsonPath("$.consents[0].version").value("2026-10"))
        .andExpect(jsonPath("$.consents[0].required").value(true))
        .andExpect(jsonPath("$.consents[0].title").value("서비스 이용약관"))
        .andExpect(jsonPath("$.consents[0].url").value("/terms/2026-10"));
  }

  @Test
  @DisplayName("가입 대기 상태가 아니면 가입 정보 입력 화면을 401 UNAUTHENTICATED로 거절한다")
  void notPending_cannotGetSignupForm() throws Exception {
    mockMvc
        .perform(get("/api/v1/auth/signup"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("가입 정보를 제출하면 201과 만든 사용자 정보로 응답한다")
  void signUp_respondsCreatedWithUser() throws Exception {
    given(signupService.signUp(any(), any())).willReturn(SIGNED_UP_USER);

    mockMvc
        .perform(signUpRequest(pendingSession()))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.user.userId").value(1))
        .andExpect(jsonPath("$.user.nickname").value("Sun"))
        .andExpect(jsonPath("$.user.avatarType").value("F"))
        .andExpect(jsonPath("$.user.currentLanguage").value(nullValue()))
        .andExpect(jsonPath("$.user.onboardingStep").value("LANG"))
        .andExpect(jsonPath("$.user.uiTheme").value("JELLY"));
  }

  @Test
  @DisplayName("가입하면 같은 세션이 그 사용자로 로그인되고 가입 대기 정보는 지워진다")
  void signUp_logsInAndClearsPendingSignup() throws Exception {
    given(signupService.signUp(any(), any())).willReturn(SIGNED_UP_USER);
    MockHttpSession session = pendingSession();

    mockMvc.perform(signUpRequest(session)).andExpect(status().isCreated());

    mockMvc
        .perform(get("/api/v1/test/me").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(1));
    assertThat(session.getAttribute(PendingSignup.SESSION_ATTRIBUTE)).isNull();
  }

  @Test
  @DisplayName("가입해 로그인할 때 세션 ID를 바꿔 가입 전 세션 ID로는 로그인 세션을 쓸 수 없게 한다")
  void signUp_changesSessionId() throws Exception {
    given(signupService.signUp(any(), any())).willReturn(SIGNED_UP_USER);
    MockHttpSession session = pendingSession();
    String sessionIdBeforeLogin = session.getId();

    mockMvc.perform(signUpRequest(session)).andExpect(status().isCreated());

    assertThat(session.getId()).isNotEqualTo(sessionIdBeforeLogin);
  }

  @Test
  @DisplayName("닉네임을 한글 2, 영문·숫자 1로 센 합계가 16을 넘으면 400 VALIDATION_FAILED로 응답한다")
  void nicknameWiderThanSixteen_isRejected() throws Exception {
    String nickname17 = "a".repeat(17);

    mockMvc
        .perform(
            post("/api/v1/auth/signup")
                .session(pendingSession())
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content(
                    "{\"nickname\": \""
                        + nickname17
                        + "\", \"avatarType\": \"F\", \"consents\": []}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("닉네임 규칙에 맞지 않는 글자가 있으면 400 VALIDATION_FAILED로 응답한다")
  void nicknameBreakingRule_isRejected() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/auth/signup")
                .session(pendingSession())
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"nickname\": \"Sun!\", \"avatarType\": \"F\", \"consents\": []}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("이미 로그인한 사용자가 가입 정보를 제출하면 409 ALREADY_SIGNED_UP으로 응답한다")
  void loggedInUser_cannotSignUpAgain() throws Exception {
    mockMvc
        .perform(signUpRequest(new MockHttpSession()).with(authentication(loggedIn(1L))))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("ALREADY_SIGNED_UP"));
  }

  @Test
  @DisplayName("가입 대기 상태가 아니면 가입 정보 제출을 401 UNAUTHENTICATED로 거절한다")
  void notPending_cannotSignUp() throws Exception {
    mockMvc
        .perform(signUpRequest(new MockHttpSession()))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("게스트가 가입하는 중이면 가입 화면에 게스트 때 고른 캐릭터와 음성 처리 동의를 담는다")
  void convertingGuest_signupFormIncludesGuest() throws Exception {
    given(signupService.findConvertingGuest(any()))
        .willReturn(Optional.of(new ConvertingGuest(AvatarType.M, true)));

    mockMvc
        .perform(get("/api/v1/auth/signup").session(pendingSession()))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.guest.avatarType").value("M"))
        .andExpect(jsonPath("$.guest.voiceConsent").value(true));
  }

  @Test
  @DisplayName("게스트가 아니면 가입 화면에 게스트 정보를 담지 않는다")
  void notConvertingGuest_signupFormHasNoGuest() throws Exception {
    mockMvc
        .perform(get("/api/v1/auth/signup").session(pendingSession()))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.guest").doesNotExist());
  }

  @Test
  @DisplayName("게스트 세션(2시간)에서 가입하면 세션 유지 시간을 회원 기준(24시간)으로 늘린다")
  void signUp_fromGuestSession_lastsLikeMember() throws Exception {
    given(signupService.signUp(any(), any())).willReturn(SIGNED_UP_USER);
    MockHttpSession session = pendingSession();
    session.setMaxInactiveInterval(2 * 60 * 60);

    mockMvc.perform(signUpRequest(session)).andExpect(status().isCreated());

    assertThat(session.getMaxInactiveInterval()).isEqualTo(24 * 60 * 60);
  }

  private static MockHttpServletRequestBuilder signUpRequest(MockHttpSession session) {
    return post("/api/v1/auth/signup")
        .session(session)
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content(
            """
            {
              "nickname": "Sun",
              "avatarType": "F",
              "consents": [
                { "consentType": "TERMS", "version": "2026-10" },
                { "consentType": "PRIVACY", "version": "2026-11" },
                { "consentType": "AGE14", "version": "2026-10" }
              ]
            }
            """);
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
