package com.elsehere.common.auth;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.time.Duration;
import java.util.List;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContext;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.core.context.SecurityContextHolderStrategy;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;
import org.springframework.security.web.context.SecurityContextRepository;
import org.springframework.stereotype.Service;

/**
 * 로그인 상태를 서버 세션에 저장한다. 이후 요청은 세션 쿠키로 같은 사용자로 인식된다.
 *
 * <p>세션 유지 시간은 가입자가 서버 설정(server.servlet.session.timeout), 게스트가 그보다 짧은
 * elsehere.guest.session-timeout이다.
 */
@Service
public class LoginSessionService {

  private final SecurityContextHolderStrategy contextHolderStrategy =
      SecurityContextHolder.getContextHolderStrategy();
  private final SecurityContextRepository contextRepository =
      new HttpSessionSecurityContextRepository();
  private final Duration guestSessionTimeout;

  public LoginSessionService(
      @Value("${elsehere.guest.session-timeout}") Duration guestSessionTimeout) {
    this.guestSessionTimeout = guestSessionTimeout;
  }

  /** 게스트로 로그인한다. 다시 들어올 수 없는 일회용 계정이라 세션을 가입자보다 짧게 유지한다. */
  public void loginGuest(
      LoginUser loginUser, HttpServletRequest request, HttpServletResponse response) {
    login(loginUser, request, response);
    request.getSession().setMaxInactiveInterval((int) guestSessionTimeout.toSeconds());
  }

  /** 로그인 전에 쓰던 세션이 있으면 ID를 바꿔, 로그인 전에 노출된 세션 ID로 로그인 세션을 쓰지 못하게 한다(세션 고정 방지). */
  public void login(LoginUser loginUser, HttpServletRequest request, HttpServletResponse response) {
    if (request.getSession(false) != null) {
      request.changeSessionId();
    }
    SecurityContext context = contextHolderStrategy.createEmptyContext();
    context.setAuthentication(
        UsernamePasswordAuthenticationToken.authenticated(loginUser, null, List.of()));
    contextHolderStrategy.setContext(context);
    contextRepository.saveContext(context, request, response);
  }

  /** 세션의 로그인 상태를 비운다. 세션 자체와 다른 세션 값은 그대로 둔다. */
  public void clearLogin(HttpServletRequest request, HttpServletResponse response) {
    SecurityContext emptyContext = contextHolderStrategy.createEmptyContext();
    contextHolderStrategy.setContext(emptyContext);
    contextRepository.saveContext(emptyContext, request, response);
  }
}
