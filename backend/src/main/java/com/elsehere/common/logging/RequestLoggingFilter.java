package com.elsehere.common.logging;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import lombok.extern.slf4j.Slf4j;
import org.slf4j.MDC;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;
import org.springframework.web.servlet.HandlerMapping;

@Slf4j
@Component
@Order(Ordered.HIGHEST_PRECEDENCE)
public class RequestLoggingFilter extends OncePerRequestFilter {

  @Override
  protected void doFilterInternal(
      HttpServletRequest request, HttpServletResponse response, FilterChain filterChain)
      throws ServletException, IOException {
    String requestId = request.getHeader("X-Request-ID");
    if (requestId == null || !requestId.matches("[a-f0-9]{32}")) {
      requestId = UUID.randomUUID().toString().replace("-", "");
    }
    response.setHeader("X-Request-ID", requestId);
    String previousRequestId = MDC.get("requestId");
    String previousUserId = MDC.get(UserLoggingFilter.USER_ID);
    MDC.put("requestId", requestId);
    long startedAt = System.nanoTime();
    boolean failed = false;
    try {
      filterChain.doFilter(request, response);
    } catch (IOException | ServletException | RuntimeException | Error exception) {
      failed = true;
      throw exception;
    } finally {
      boolean successfulHealthCheck =
          !failed
              && response.getStatus() == HttpServletResponse.SC_OK
              && "GET".equals(request.getMethod())
              && (request.getContextPath() + "/api/health").equals(request.getRequestURI());
      if (!successfulHealthCheck) {
        Object route = request.getAttribute(HandlerMapping.BEST_MATCHING_PATTERN_ATTRIBUTE);
        log.info(
            "http_request method={} route={} status={} duration_ms={}",
            request.getMethod(),
            route == null ? "/unmatched" : route,
            failed ? HttpServletResponse.SC_INTERNAL_SERVER_ERROR : response.getStatus(),
            TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - startedAt));
      }
      restore("requestId", previousRequestId);
      // 보안 필터 안쪽(UserLoggingFilter)에서 넣은 사용자 ID도 요청 요약을 남긴 뒤 정리한다.
      restore(UserLoggingFilter.USER_ID, previousUserId);
    }
  }

  private static void restore(String key, String previousValue) {
    if (previousValue == null) {
      MDC.remove(key);
    } else {
      MDC.put(key, previousValue);
    }
  }
}
