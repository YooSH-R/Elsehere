package com.elsehere.common.error;

import static org.assertj.core.api.Assertions.assertThat;

import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;

class ErrorLoggingPrivacyTest {

  @Test
  void unexpectedError_logsTypeAndLocationWithoutOriginalMessage() {
    Logger logger = (Logger) LoggerFactory.getLogger(GlobalExceptionHandler.class);
    ListAppender<ILoggingEvent> appender = new ListAppender<>();
    appender.start();
    logger.addAppender(appender);
    try {
      new GlobalExceptionHandler().handleOther(new IllegalStateException("private-conversation"));

      assertThat(appender.list)
          .singleElement()
          .satisfies(
              event -> {
                assertThat(event.getFormattedMessage())
                    .contains("IllegalStateException", "ErrorLoggingPrivacyTest.java")
                    .doesNotContain("private-conversation");
                assertThat(event.getThrowableProxy()).isNull();
              });
    } finally {
      logger.detachAppender(appender);
      appender.stop();
    }
  }
}
