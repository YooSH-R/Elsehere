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
    MDC.put("requestId", requestId);
    long startedAt = System.nanoTime();
    boolean failed = false;
    try {
      filterChain.doFilter(request, response);
    } catch (IOException | ServletException | RuntimeException | Error exception) {
      failed = true;
      throw exception;
    } finally {
      Object route = request.getAttribute(HandlerMapping.BEST_MATCHING_PATTERN_ATTRIBUTE);
      log.info(
          "http_request method={} route={} status={} duration_ms={}",
          request.getMethod(),
          route == null ? "/unmatched" : route,
          failed ? HttpServletResponse.SC_INTERNAL_SERVER_ERROR : response.getStatus(),
          TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - startedAt));
      if (previousRequestId == null) {
        MDC.remove("requestId");
      } else {
        MDC.put("requestId", previousRequestId);
      }
    }
  }
}
