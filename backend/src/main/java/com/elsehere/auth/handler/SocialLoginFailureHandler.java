package com.elsehere.auth.handler;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.security.core.AuthenticationException;
import org.springframework.security.web.authentication.AuthenticationFailureHandler;
import org.springframework.stereotype.Component;
import org.springframework.web.util.UriComponentsBuilder;

/** 제공자 인증이 실패하거나 사용자가 취소하면 클라이언트로 돌려보내고 {@code loginError=true}로 실패를 알린다. */
@Slf4j
@Component
public class SocialLoginFailureHandler implements AuthenticationFailureHandler {

  private final String failureRedirectUrl;

  public SocialLoginFailureHandler(@Value("${elsehere.client-url}") String clientUrl) {
    this.failureRedirectUrl =
        UriComponentsBuilder.fromUriString(clientUrl).queryParam("loginError", true).toUriString();
  }

  @Override
  public void onAuthenticationFailure(
      HttpServletRequest request, HttpServletResponse response, AuthenticationException exception)
      throws IOException {
    log.info("소셜 로그인 실패: {}", exception.getMessage());
    response.sendRedirect(failureRedirectUrl);
  }
}
