package com.elsehere.common.auth;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.util.List;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContext;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.core.context.SecurityContextHolderStrategy;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;
import org.springframework.security.web.context.SecurityContextRepository;
import org.springframework.stereotype.Service;

/** 로그인 상태를 서버 세션에 저장한다. 이후 요청은 세션 쿠키로 같은 사용자로 인식된다. */
@Service
public class LoginSessionService {

  private final SecurityContextHolderStrategy contextHolderStrategy =
      SecurityContextHolder.getContextHolderStrategy();
  private final SecurityContextRepository contextRepository =
      new HttpSessionSecurityContextRepository();

  public void login(LoginUser loginUser, HttpServletRequest request, HttpServletResponse response) {
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
