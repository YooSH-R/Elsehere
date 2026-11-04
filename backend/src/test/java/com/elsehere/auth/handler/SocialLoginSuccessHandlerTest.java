package com.elsehere.auth.handler;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.auth.service.PendingSignup;
import com.elsehere.auth.service.SocialLoginService;
import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.core.context.SecurityContext;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.core.context.SecurityContextImpl;
import org.springframework.security.oauth2.client.authentication.OAuth2AuthenticationToken;
import org.springframework.security.oauth2.core.user.DefaultOAuth2User;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;

@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.client-url=https://client.test/"
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  SocialLoginService.class,
  LoginSessionService.class,
  SocialLoginSuccessHandler.class
})
class SocialLoginSuccessHandlerTest {

  private static final String CLIENT_URL = "https://client.test/";

  @Autowired private EntityManager entityManager;

  @Autowired private SocialLoginSuccessHandler successHandler;

  @Autowired private LoginSessionService loginSessionService;

  // 로그인 처리가 현재 스레드의 SecurityContext도 바꾸므로 다음 테스트에 남지 않게 지운다.
  @AfterEach
  void clearSecurityContext() {
    SecurityContextHolder.clearContext();
  }

  @Test
  @DisplayName("가입한 Google 계정이면 그 사용자로 로그인한 세션을 만들고 클라이언트로 보낸다")
  void signedUpAccount_logsInAndRedirectsToClient() throws Exception {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    MockHttpServletRequest request = new MockHttpServletRequest();
    MockHttpServletResponse response = new MockHttpServletResponse();

    successHandler.onAuthenticationSuccess(request, response, googleLogin("google-sub-1"));

    assertThat(loggedInPrincipal(request)).isEqualTo(new LoginUser(userId));
    assertThat(response.getRedirectedUrl()).isEqualTo(CLIENT_URL);
  }

  @Test
  @DisplayName("처음 로그인한 계정이면 로그인 상태로 두지 않고 가입 대기 정보만 세션에 둔 뒤 클라이언트로 보낸다")
  void newAccount_keepsPendingSignupWithoutLogin() throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest();
    MockHttpServletResponse response = new MockHttpServletResponse();
    // Spring Security는 성공 처리기를 부르기 전에 제공자 인증 정보를 세션에 저장해 둔다.
    request
        .getSession()
        .setAttribute(
            HttpSessionSecurityContextRepository.SPRING_SECURITY_CONTEXT_KEY,
            new SecurityContextImpl(googleLogin("google-sub-new")));

    successHandler.onAuthenticationSuccess(request, response, googleLogin("google-sub-new"));

    assertThat(loggedInPrincipal(request)).isNull();
    assertThat(request.getSession().getAttribute(PendingSignup.SESSION_ATTRIBUTE))
        .isEqualTo(new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com"));
    assertThat(response.getRedirectedUrl()).isEqualTo(CLIENT_URL);
  }

  @Test
  @DisplayName("가입한 계정으로 로그인하면 앞서 남아 있던 다른 계정의 가입 대기 정보를 지운다")
  void signedUpAccount_removesEarlierPendingSignup() throws Exception {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    MockHttpServletRequest request = new MockHttpServletRequest();
    request
        .getSession()
        .setAttribute(
            PendingSignup.SESSION_ATTRIBUTE,
            new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "new@example.com"));

    successHandler.onAuthenticationSuccess(
        request, new MockHttpServletResponse(), googleLogin("google-sub-1"));

    assertThat(request.getSession().getAttribute(PendingSignup.SESSION_ATTRIBUTE)).isNull();
  }

  @Test
  @DisplayName("게스트로 로그인한 세션에서 처음 쓰는 Google 계정으로 로그인하면 가입 대기 정보에 게스트를 남겨 가입 때 회원으로 바꾸게 한다")
  void guestSession_newAccount_keepsGuestInPendingSignup() throws Exception {
    Long guestId = insertGuest();
    MockHttpServletRequest request = guestLoggedInRequest(guestId);
    signInWithGoogleProvider(request, "google-sub-new");

    successHandler.onAuthenticationSuccess(
        request, new MockHttpServletResponse(), googleLogin("google-sub-new"));

    assertThat(loggedInPrincipal(request)).isNull();
    assertThat(request.getSession().getAttribute(PendingSignup.SESSION_ATTRIBUTE))
        .isEqualTo(
            new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com", guestId));
  }

  @Test
  @DisplayName("게스트 세션에서 이미 가입한 Google 계정으로 로그인하면 그 계정으로 로그인하고 게스트 기록을 합치지 않았다고 알린다")
  void guestSession_signedUpAccount_logsInAndTellsExistingAccount() throws Exception {
    Long memberId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1", "Sun");
    MockHttpServletRequest request = guestLoggedInRequest(insertGuest());
    signInWithGoogleProvider(request, "google-sub-1");
    MockHttpServletResponse response = new MockHttpServletResponse();

    successHandler.onAuthenticationSuccess(request, response, googleLogin("google-sub-1"));

    assertThat(loggedInPrincipal(request)).isEqualTo(new LoginUser(memberId));
    assertThat(response.getRedirectedUrl()).isEqualTo(CLIENT_URL + "?existingAccount=true");
  }

  @Test
  @DisplayName("게스트 세션에서 가입한 계정으로 로그인하면 세션 유지 시간을 게스트(2시간)에서 회원(24시간)으로 늘린다")
  void guestSession_signedUpAccount_sessionLastsLikeMember() throws Exception {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1", "Sun");
    MockHttpServletRequest request = guestLoggedInRequest(insertGuest());
    signInWithGoogleProvider(request, "google-sub-1");

    successHandler.onAuthenticationSuccess(
        request, new MockHttpServletResponse(), googleLogin("google-sub-1"));

    assertThat(request.getSession().getMaxInactiveInterval()).isEqualTo(24 * 60 * 60);
  }

  private Long insertGuest() {
    return UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid", "guest-000001");
  }

  private MockHttpServletRequest guestLoggedInRequest(Long guestId) {
    MockHttpServletRequest request = new MockHttpServletRequest();
    loginSessionService.loginGuest(new LoginUser(guestId), request, new MockHttpServletResponse());
    return request;
  }

  // Spring Security는 성공 처리기를 부르기 전에 세션의 로그인 정보를 제공자 인증 정보로 바꿔 둔다.
  private static void signInWithGoogleProvider(MockHttpServletRequest request, String subject) {
    request
        .getSession()
        .setAttribute(
            HttpSessionSecurityContextRepository.SPRING_SECURITY_CONTEXT_KEY,
            new SecurityContextImpl(googleLogin(subject)));
  }

  private static OAuth2AuthenticationToken googleLogin(String subject) {
    DefaultOAuth2User googleUser =
        new DefaultOAuth2User(List.of(), Map.of("sub", subject, "email", "sun@example.com"), "sub");
    return new OAuth2AuthenticationToken(googleUser, List.of(), "google");
  }

  private static Object loggedInPrincipal(MockHttpServletRequest request) {
    SecurityContext context =
        (SecurityContext)
            request
                .getSession()
                .getAttribute(HttpSessionSecurityContextRepository.SPRING_SECURITY_CONTEXT_KEY);
    if (context == null || context.getAuthentication() == null) {
      return null;
    }
    return context.getAuthentication().getPrincipal();
  }
}
