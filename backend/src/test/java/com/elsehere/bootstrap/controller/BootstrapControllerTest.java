package com.elsehere.bootstrap.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.bootstrap.service.BootstrapService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextImpl;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 사용자 상태 조회는 BootstrapServiceTest에서 DB와 함께 검증하고, 여기서는 로그인 상태별 응답만 본다.
@SecuredWebMvcTest(controllers = {BootstrapController.class, LoginUserTestController.class})
class BootstrapControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private BootstrapService bootstrapService;

  @Test
  @DisplayName("로그인한 사용자면 SIGNED_IN과 사용자 정보, 재접속 장소를 돌려준다")
  void signedInUser_getsSignedInState() throws Exception {
    UserResponse user = new UserResponse(1L, "Sun", AvatarType.F, "en-US", OnboardingStep.DONE);
    given(bootstrapService.findSignedIn(1L))
        .willReturn(Optional.of(BootstrapResponse.signedIn(user, "TOWN")));

    mockMvc
        .perform(get("/api/v1/bootstrap").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.authState").value("SIGNED_IN"))
        .andExpect(jsonPath("$.user.userId").value(1))
        .andExpect(jsonPath("$.user.onboardingStep").value("DONE"))
        .andExpect(jsonPath("$.lastSceneCode").value("TOWN"));
  }

  @Test
  @DisplayName("가입 대기 상태면 200으로 SIGNUP_REQUIRED만 돌려준다")
  void pendingSignup_getsSignupRequiredState() throws Exception {
    MockHttpSession pendingSession = new MockHttpSession();
    pendingSession.setAttribute(
        PendingSignup.SESSION_ATTRIBUTE,
        new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com"));

    mockMvc
        .perform(get("/api/v1/bootstrap").session(pendingSession))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.authState").value("SIGNUP_REQUIRED"))
        .andExpect(jsonPath("$.user").doesNotExist());
  }

  @Test
  @DisplayName("로그인하지 않았으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/bootstrap"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("세션의 사용자가 DB에 없으면 401로 응답하고 세션의 로그인 상태를 비운다")
  void missingUser_respondsUnauthenticatedAndClearsLogin() throws Exception {
    given(bootstrapService.findSignedIn(999L)).willReturn(Optional.empty());
    MockHttpSession session = new MockHttpSession();
    session.setAttribute(
        HttpSessionSecurityContextRepository.SPRING_SECURITY_CONTEXT_KEY,
        new SecurityContextImpl(loggedIn(999L)));

    mockMvc
        .perform(get("/api/v1/bootstrap").session(session))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));

    mockMvc.perform(get("/api/v1/test/me").session(session)).andExpect(status().isUnauthorized());
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
