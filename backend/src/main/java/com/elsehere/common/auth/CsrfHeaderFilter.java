package com.elsehere.common.auth;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.web.filter.OncePerRequestFilter;
import org.springframework.web.servlet.HandlerExceptionResolver;

/**
 * 상태를 바꾸는 요청은 {@code X-Requested-With} 헤더가 있을 때만 처리해 요청 위조(CSRF)를 막는다.
 *
 * <p>브라우저는 다른 사이트의 페이지가 사용자 정의 헤더를 붙여 보내는 요청을 CORS 허용 없이 전송하지 않는다. 세션 쿠키의 {@code SameSite=Lax}와 함께
 * 쓴다. 보안 필터 체인에만 등록하므로 빈으로 만들지 않는다.
 */
@RequiredArgsConstructor
public class CsrfHeaderFilter extends OncePerRequestFilter {

  public static final String HEADER_NAME = "X-Requested-With";

  private static final Set<String> SAFE_METHODS = Set.of("GET", "HEAD", "OPTIONS", "TRACE");

  private final HandlerExceptionResolver exceptionResolver;

  @Override
  protected void doFilterInternal(
      HttpServletRequest request, HttpServletResponse response, FilterChain filterChain)
      throws ServletException, IOException {
    boolean changesState = !SAFE_METHODS.contains(request.getMethod());
    if (changesState && request.getHeader(HEADER_NAME) == null) {
      exceptionResolver.resolveException(
          request, response, null, new BusinessException(ErrorCode.CSRF_HEADER_REQUIRED));
      return;
    }
    filterChain.doFilter(request, response);
  }
}
