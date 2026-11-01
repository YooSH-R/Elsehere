package com.elsehere.common.logging;

import com.elsehere.common.auth.LoginUser;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import org.slf4j.MDC;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.filter.OncePerRequestFilter;

/**
 * 로그인 사용자 ID(users.id)를 로그 문맥(MDC)에 넣어 이 요청의 모든 로그 줄에 user_id가 붙게 한다. 보안 필터 체인에서 인증을 마친 뒤에
 * 실행한다(SecurityConfig).
 *
 * <p>요청 요약(http_request)에도 붙도록 여기서는 지우지 않고, 가장 바깥의 {@link RequestLoggingFilter}가 요청이 끝날 때 정리한다.
 * 이메일·닉네임 같은 개인정보는 넣지 않는다.
 */
public class UserLoggingFilter extends OncePerRequestFilter {

  static final String USER_ID = "userId";

  @Override
  protected void doFilterInternal(
      HttpServletRequest request, HttpServletResponse response, FilterChain filterChain)
      throws ServletException, IOException {
    Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
    if (authentication != null && authentication.getPrincipal() instanceof LoginUser loginUser) {
      MDC.put(USER_ID, String.valueOf(loginUser.userId()));
    }
    filterChain.doFilter(request, response);
  }
}
