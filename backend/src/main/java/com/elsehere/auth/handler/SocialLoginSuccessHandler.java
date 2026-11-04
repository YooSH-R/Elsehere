package com.elsehere.auth.handler;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.auth.service.SocialLoginService;
import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.entity.AuthProvider;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.Locale;
import java.util.Optional;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.security.core.Authentication;
import org.springframework.security.oauth2.client.authentication.OAuth2AuthenticationToken;
import org.springframework.security.web.authentication.AuthenticationSuccessHandler;
import org.springframework.stereotype.Component;
import org.springframework.web.util.UriComponentsBuilder;

/**
 * 소셜 로그인 제공자 인증을 마친 뒤(AUTH-02) 우리 서비스의 로그인 상태를 정하고 클라이언트로 돌려보낸다.
 *
 * <p>Spring Security가 세션에 넣은 제공자 인증 정보는 우리 서비스의 로그인 정보({@link LoginUser})로 바꾼다.
 */
@Component
public class SocialLoginSuccessHandler implements AuthenticationSuccessHandler {

  private final SocialLoginService socialLoginService;
  private final LoginSessionService loginSessionService;
  private final String clientUrl;

  public SocialLoginSuccessHandler(
      SocialLoginService socialLoginService,
      LoginSessionService loginSessionService,
      @Value("${elsehere.client-url}") String clientUrl) {
    this.socialLoginService = socialLoginService;
    this.loginSessionService = loginSessionService;
    this.clientUrl = clientUrl;
  }

  @Override
  public void onAuthenticationSuccess(
      HttpServletRequest request, HttpServletResponse response, Authentication authentication)
      throws IOException {
    OAuth2AuthenticationToken providerLogin = (OAuth2AuthenticationToken) authentication;
    AuthProvider provider =
        AuthProvider.valueOf(
            providerLogin.getAuthorizedClientRegistrationId().toUpperCase(Locale.ROOT));
    String providerUid = providerLogin.getPrincipal().getName();

    Long guestUserId = loginSessionService.findGuestUserId(request).orElse(null);
    Optional<LoginUser> loginUser = socialLoginService.findLoginUser(provider, providerUid);
    if (loginUser.isPresent()) {
      loginSessionService.login(loginUser.get(), request, response);
      request.getSession().removeAttribute(PendingSignup.SESSION_ATTRIBUTE);
      // 이미 가입한 계정이면 그 계정으로 들어가고 게스트 기록은 합치지 않는다. 화면이 그 사실을 알리게 한다.
      response.sendRedirect(guestUserId == null ? clientUrl : existingAccountUrl());
      return;
    }

    // 가입 정보(닉네임·캐릭터·약관)를 받기 전에는 사용자를 만들 수 없으므로 로그인 상태로 두지 않는다.
    // 제공자 계정 정보만 세션에 두고 가입 정보 제출(AUTH-04)을 기다린다. 게스트였으면 가입할 때 그 게스트를 회원으로 바꾼다.
    loginSessionService.clearLogin(request, response);
    String email = providerLogin.getPrincipal().getAttribute("email");
    request
        .getSession()
        .setAttribute(
            PendingSignup.SESSION_ATTRIBUTE,
            new PendingSignup(provider, providerUid, email, guestUserId));
    response.sendRedirect(clientUrl);
  }

  private String existingAccountUrl() {
    return UriComponentsBuilder.fromUriString(clientUrl)
        .queryParam("existingAccount", true)
        .toUriString();
  }
}
