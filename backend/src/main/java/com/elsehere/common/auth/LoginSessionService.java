package com.elsehere.common.auth;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.servlet.http.HttpSession;
import java.time.Duration;
import java.util.List;
import java.util.Optional;
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

  // Google 로그인은 성공 처리 전에 세션의 로그인 정보를 제공자 인증 정보로 바꾼다. 그 뒤에도 게스트였는지 알 수 있도록 따로 남긴다.
  private static final String GUEST_USER_ID = "GUEST_USER_ID";

  private final SecurityContextHolderStrategy contextHolderStrategy =
      SecurityContextHolder.getContextHolderStrategy();
  private final SecurityContextRepository contextRepository =
      new HttpSessionSecurityContextRepository();
  private final Duration memberSessionTimeout;
  private final Duration guestSessionTimeout;

  public LoginSessionService(
      @Value("${server.servlet.session.timeout}") Duration memberSessionTimeout,
      @Value("${elsehere.guest.session-timeout}") Duration guestSessionTimeout) {
    this.memberSessionTimeout = memberSessionTimeout;
    this.guestSessionTimeout = guestSessionTimeout;
  }

  /** 게스트로 로그인한다. 다시 들어올 수 없는 일회용 계정이라 세션을 가입자보다 짧게 유지한다. */
  public void loginGuest(
      LoginUser loginUser, HttpServletRequest request, HttpServletResponse response) {
    login(loginUser, request, response);
    HttpSession session = request.getSession();
    session.setMaxInactiveInterval((int) guestSessionTimeout.toSeconds());
    session.setAttribute(GUEST_USER_ID, loginUser.userId());
  }

  /**
   * 회원으로 로그인한다. 로그인 전에 쓰던 세션이 있으면 ID를 바꿔, 로그인 전에 노출된 세션 ID로 로그인 세션을 쓰지 못하게 한다(세션 고정 방지). 게스트로 쓰던
   * 세션이면 유지 시간을 회원 기준으로 되돌린다.
   */
  public void login(LoginUser loginUser, HttpServletRequest request, HttpServletResponse response) {
    if (request.getSession(false) != null) {
      request.changeSessionId();
    }
    SecurityContext context = contextHolderStrategy.createEmptyContext();
    context.setAuthentication(
        UsernamePasswordAuthenticationToken.authenticated(loginUser, null, List.of()));
    contextHolderStrategy.setContext(context);
    contextRepository.saveContext(context, request, response);
    HttpSession session = request.getSession();
    session.setMaxInactiveInterval((int) memberSessionTimeout.toSeconds());
    session.removeAttribute(GUEST_USER_ID);
  }

  /** 이 세션에서 게스트로 로그인해 아직 회원으로 로그인하지 않았으면 그 게스트 사용자 ID. */
  public Optional<Long> findGuestUserId(HttpServletRequest request) {
    HttpSession session = request.getSession(false);
    if (session == null) {
      return Optional.empty();
    }
    return Optional.ofNullable((Long) session.getAttribute(GUEST_USER_ID));
  }

  /** 세션의 로그인 상태를 비운다. 세션 자체와 다른 세션 값은 그대로 둔다. */
  public void clearLogin(HttpServletRequest request, HttpServletResponse response) {
    SecurityContext emptyContext = contextHolderStrategy.createEmptyContext();
    contextHolderStrategy.setContext(emptyContext);
    contextRepository.saveContext(emptyContext, request, response);
  }
}
