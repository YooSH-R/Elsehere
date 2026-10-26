package com.elsehere.auth.handler;

import static org.assertj.core.api.Assertions.assertThat;

import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.authentication.BadCredentialsException;

class SocialLoginFailureHandlerTest {

  @Test
  void loginFailure_keepsRedirectAndOmitsProviderResponseFromLog() throws Exception {
    Logger logger = (Logger) LoggerFactory.getLogger(SocialLoginFailureHandler.class);
    ListAppender<ILoggingEvent> appender = new ListAppender<>();
    appender.start();
    logger.addAppender(appender);
    try {
      MockHttpServletResponse response = new MockHttpServletResponse();
      new SocialLoginFailureHandler("/")
          .onAuthenticationFailure(
              new MockHttpServletRequest(),
              response,
              new BadCredentialsException("private-oauth-response"));

      assertThat(response.getRedirectedUrl()).isEqualTo("/?loginError=true");
      assertThat(appender.list)
          .singleElement()
          .satisfies(
              event -> {
                assertThat(event.getFormattedMessage())
                    .contains("BadCredentialsException")
                    .doesNotContain("private-oauth-response");
                assertThat(event.getThrowableProxy()).isNull();
              });
    } finally {
      logger.detachAppender(appender);
      appender.stop();
    }
  }
}
