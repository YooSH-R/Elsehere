package com.elsehere.admin.service;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.util.List;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContext;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.core.context.SecurityContextHolderStrategy;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;
import org.springframework.security.web.context.SecurityContextRepository;
import org.springframework.stereotype.Component;

/**
 * 어드민 로그인 상태를 서버 세션에 저장한다. 게임 로그인과 같은 세션 쿠키를 쓰지만 세션의 다른 칸({@link #SESSION_ATTRIBUTE})에 두어, 게임 보안
 * 설정은 어드민 로그인을 읽지 않고 어드민 보안 설정(AdminSecurityConfig)은 게임 로그인을 읽지 않는다.
 */
@Component
public class AdminLoginSession {

  /** 게임 로그인 칸({@link HttpSessionSecurityContextRepository#SPRING_SECURITY_CONTEXT_KEY})과 다른 이름 */
  public static final String SESSION_ATTRIBUTE = "ELSEHERE_ADMIN_SECURITY_CONTEXT";

  private final SecurityContextHolderStrategy contextHolderStrategy =
      SecurityContextHolder.getContextHolderStrategy();
  private final HttpSessionSecurityContextRepository contextRepository =
      new HttpSessionSecurityContextRepository();

  public AdminLoginSession() {
    contextRepository.setSpringSecurityContextKey(SESSION_ATTRIBUTE);
  }

  /** 어드민 보안 설정이 요청마다 로그인 상태를 읽는 곳 */
  public SecurityContextRepository contextRepository() {
    return contextRepository;
  }

  /** 로그인 전에 쓰던 세션이 있으면 ID를 바꿔, 로그인 전에 노출된 세션 ID로 어드민 세션을 쓰지 못하게 한다(세션 고정 방지). */
  public void login(
      AdminLogin adminLogin, HttpServletRequest request, HttpServletResponse response) {
    if (request.getSession(false) != null) {
      request.changeSessionId();
    }
    SecurityContext context = contextHolderStrategy.createEmptyContext();
    context.setAuthentication(
        UsernamePasswordAuthenticationToken.authenticated(adminLogin, null, List.of()));
    contextHolderStrategy.setContext(context);
    contextRepository.saveContext(context, request, response);
  }

  /** 어드민 로그인만 지운다. 같은 세션의 게임 로그인은 그대로 둔다. */
  public void logout(HttpServletRequest request, HttpServletResponse response) {
    SecurityContext emptyContext = contextHolderStrategy.createEmptyContext();
    contextHolderStrategy.setContext(emptyContext);
    contextRepository.saveContext(emptyContext, request, response);
  }
}
