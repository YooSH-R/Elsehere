package com.elsehere.common.config;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.redirectedUrl;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.entity.AuthProvider;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.web.util.UriComponents;
import org.springframework.web.util.UriComponentsBuilder;

@SecuredWebMvcTest(controllers = LoginUserTestController.class)
class SecurityConfigTest {

  @Autowired private MockMvc mockMvc;

  @Test
  @DisplayName("로그인하지 않고 API를 호출하면 401 UNAUTHENTICATED로 응답한다")
  void unauthenticatedRequest_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/test/me"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"))
        .andExpect(jsonPath("$.error.message").value(ErrorCode.UNAUTHENTICATED.getMessage()));
  }

  @Test
  @DisplayName("가입 대기 중인 사용자가 다른 API를 호출하면 403 SIGNUP_REQUIRED로 응답한다")
  void pendingSignupRequest_respondsSignupRequired() throws Exception {
    MockHttpSession pendingSession = new MockHttpSession();
    pendingSession.setAttribute(
        PendingSignup.SESSION_ATTRIBUTE,
        new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com"));

    mockMvc
        .perform(get("/api/v1/test/me").session(pendingSession))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("SIGNUP_REQUIRED"));
  }

  @Test
  @DisplayName("로그인한 사용자는 컨트롤러에서 자기 사용자 ID를 받는다")
  void authenticatedRequest_injectsLoginUser() throws Exception {
    mockMvc
        .perform(get("/api/v1/test/me").with(authentication(loggedIn(7L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(7));
  }

  @Test
  @DisplayName("상태를 바꾸는 요청에 X-Requested-With 헤더가 없으면 403 CSRF_HEADER_REQUIRED로 거절한다")
  void stateChangingRequestWithoutHeader_isRejected() throws Exception {
    mockMvc
        .perform(post("/api/v1/test/me").with(authentication(loggedIn(7L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 있는 상태 변경 요청은 처리한다")
  void stateChangingRequestWithHeader_isProcessed() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/test/me")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(7L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(7));
  }

  @Test
  @DisplayName("Google 로그인 시작(AUTH-01)은 Google 인증 화면으로 보내고 콜백 주소로 AUTH-02 경로를 알려 준다")
  void googleLoginStart_redirectsToGoogleWithCallbackPath() throws Exception {
    MvcResult result =
        mockMvc
            .perform(get("/api/v1/auth/login/google"))
            .andExpect(status().is3xxRedirection())
            .andReturn();

    UriComponents redirect =
        UriComponentsBuilder.fromUriString(result.getResponse().getRedirectedUrl()).build();
    String callbackUri =
        URLDecoder.decode(
            redirect.getQueryParams().getFirst("redirect_uri"), StandardCharsets.UTF_8);
    assertThat(redirect.getHost()).isEqualTo("accounts.google.com");
    assertThat(callbackUri).isEqualTo("http://localhost/api/v1/auth/callback/google");
  }

  @Test
  @DisplayName("Google 로그인이 실패하거나 취소되면 클라이언트 주소로 실패 표시를 붙여 돌려보낸다")
  void googleLoginFailure_redirectsToClientWithError() throws Exception {
    // 로그인 시작(AUTH-01) 없이 콜백만 오면 인증 요청 정보가 없어 실패한다. 사용자가 동의를 취소해도 같은 처리기로 간다.
    mockMvc
        .perform(get("/api/v1/auth/callback/google").param("code", "code").param("state", "state"))
        .andExpect(status().is3xxRedirection())
        .andExpect(redirectedUrl("/?loginError=true"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
