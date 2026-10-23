package com.elsehere.auth.handler;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.servlet.http.HttpSession;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.security.core.AuthenticationException;
import org.springframework.security.web.AuthenticationEntryPoint;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.HandlerExceptionResolver;

/**
 * 로그인하지 않은 요청을 거절한다. Google 로그인만 마치고 가입 정보를 내지 않은 사용자는 {@code 403 SIGNUP_REQUIRED}, 그 밖에는 {@code
 * 401 UNAUTHENTICATED}로 응답해 클라이언트가 가입 화면과 로그인 화면을 구분하게 한다.
 */
@Component
public class LoginRequiredEntryPoint implements AuthenticationEntryPoint {

  private final HandlerExceptionResolver exceptionResolver;

  public LoginRequiredEntryPoint(
      @Qualifier("handlerExceptionResolver") HandlerExceptionResolver exceptionResolver) {
    this.exceptionResolver = exceptionResolver;
  }

  @Override
  public void commence(
      HttpServletRequest request,
      HttpServletResponse response,
      AuthenticationException authenticationException) {
    ErrorCode errorCode =
        isPendingSignup(request) ? ErrorCode.SIGNUP_REQUIRED : ErrorCode.UNAUTHENTICATED;
    exceptionResolver.resolveException(request, response, null, new BusinessException(errorCode));
  }

  private static boolean isPendingSignup(HttpServletRequest request) {
    HttpSession session = request.getSession(false);
    return session != null && session.getAttribute(PendingSignup.SESSION_ATTRIBUTE) != null;
  }
}
