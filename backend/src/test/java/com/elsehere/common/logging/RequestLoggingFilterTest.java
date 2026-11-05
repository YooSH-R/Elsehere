package com.elsehere.common.logging;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.web.servlet.HandlerMapping;

class RequestLoggingFilterTest {

  private final RequestLoggingFilter filter = new RequestLoggingFilter();
  private final Logger logger = (Logger) LoggerFactory.getLogger(RequestLoggingFilter.class);
  private final ListAppender<ILoggingEvent> requestLogs =
      new ListAppender<>() {
        @Override
        protected void append(ILoggingEvent event) {
          event.prepareForDeferredProcessing();
          super.append(event);
        }
      };

  @BeforeEach
  void collectRequestLogs() {
    requestLogs.start();
    logger.addAppender(requestLogs);
  }

  @AfterEach
  void releaseRequestLogs() {
    logger.detachAppender(requestLogs);
    requestLogs.stop();
    MDC.clear();
  }

  @Test
  void successfulHealthCheck_keepsRequestIdWithoutWritingRequestLog() throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/health");
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(request, response, (incoming, outgoing) -> {});

    assertThat(response.getHeader("X-Request-ID")).matches("[a-f0-9]{32}");
    assertThat(requestLogs.list).isEmpty();
    assertThat(MDC.get("requestId")).isNull();
  }

  @Test
  void failedHealthCheck_isLogged() throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/health");
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(request, response, (incoming, outgoing) -> response.setStatus(503));

    assertThat(requestLogs.list)
        .singleElement()
        .satisfies(event -> assertThat(event.getFormattedMessage()).contains("status=503"));
  }

  @Test
  void requestWithoutId_returnsGeneratedId() throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/v1/bootstrap");
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(request, response, (incomingRequest, outgoingResponse) -> {});

    assertThat(response.getHeader("X-Request-ID")).matches("[a-f0-9]{32}");
  }

  @Test
  void proxyRequestId_isUsedDuringRequestAndReturned() throws Exception {
    String requestId = "1234567890abcdef1234567890abcdef";
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/v1/bootstrap");
    request.addHeader("X-Request-ID", requestId);
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(
        request,
        response,
        (incomingRequest, outgoingResponse) ->
            assertThat(MDC.get("requestId")).isEqualTo(requestId));

    assertThat(response.getHeader("X-Request-ID")).isEqualTo(requestId);
    assertThat(MDC.get("requestId")).isNull();
  }

  @ParameterizedTest
  @ValueSource(
      strings = {"", "client-secret", "abc\r\nforged=true", "1234567890abcdef1234567890abcdef0"})
  void invalidRequestId_isReplaced(String suppliedId) throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/v1/bootstrap");
    request.addHeader("X-Request-ID", suppliedId);
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(request, response, (incomingRequest, outgoingResponse) -> {});

    assertThat(response.getHeader("X-Request-ID")).matches("[a-f0-9]{32}").isNotEqualTo(suppliedId);
  }

  @Test
  void requestLog_containsRouteStatusAndDurationWithoutSensitiveInput() throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest("POST", "/test/private-person");
    request.setQueryString("token=query-secret");
    request.addHeader("Authorization", "Bearer header-secret");
    request.setCookies(new Cookie("JSESSIONID", "cookie-secret"));
    request.setContent("private-conversation".getBytes(java.nio.charset.StandardCharsets.UTF_8));
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(
        request,
        response,
        (incomingRequest, outgoingResponse) -> {
          request.setAttribute(HandlerMapping.BEST_MATCHING_PATTERN_ATTRIBUTE, "/test/{id}");
          response.setStatus(400);
        });

    assertThat(requestLogs.list).hasSize(1);
    ILoggingEvent event = requestLogs.list.getFirst();
    assertThat(event.getFormattedMessage())
        .matches("http_request method=POST route=/test/\\{id} status=400 duration_ms=\\d+")
        .doesNotContain(
            "private-person",
            "query-secret",
            "header-secret",
            "cookie-secret",
            "private-conversation");
    assertThat(event.getMDCPropertyMap())
        .containsEntry("requestId", response.getHeader("X-Request-ID"));
  }

  // 사용자 ID는 보안 필터 안쪽(UserLoggingFilter)에서 넣는다. 요청 요약에도 붙고, 요청이 끝나면 다음 요청에 남지 않는다.
  @Test
  void requestLog_carriesUserIdSetInsideAndClearsItAfterwards() throws Exception {
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/v1/vocab");
    MockHttpServletResponse response = new MockHttpServletResponse();

    filter.doFilter(
        request, response, (incomingRequest, outgoingResponse) -> MDC.put("userId", "7"));

    assertThat(requestLogs.list.getFirst().getMDCPropertyMap()).containsEntry("userId", "7");
    assertThat(MDC.get("userId")).isNull();
  }

  @Test
  void unhandledFailure_isLoggedAsServerErrorAndRestoresOuterContext() {
    MDC.put("requestId", "outer-context");
    MockHttpServletRequest request = new MockHttpServletRequest("GET", "/private-path");
    MockHttpServletResponse response = new MockHttpServletResponse();

    assertThatThrownBy(
            () ->
                filter.doFilter(
                    request,
                    response,
                    (incomingRequest, outgoingResponse) -> {
                      throw new ServletException("private-error-message");
                    }))
        .isInstanceOf(ServletException.class);

    assertThat(MDC.get("requestId")).isEqualTo("outer-context");
    assertThat(requestLogs.list)
        .singleElement()
        .satisfies(
            event -> {
              assertThat(event.getFormattedMessage())
                  .contains("route=/unmatched status=500")
                  .doesNotContain("private-path", "private-error-message");
              assertThat(event.getThrowableProxy()).isNull();
            });
  }
}
