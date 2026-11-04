package com.elsehere.common.error;

import static org.assertj.core.api.Assertions.assertThat;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import com.elsehere.common.logging.LogCapture;
import java.util.Map;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;

class ErrorLoggingPrivacyTest {

  @Test
  void businessError_logsCodeAndStatusAtDebugWithoutMessage() {
    try (LogCapture logs = LogCapture.atDebug(GlobalExceptionHandler.class)) {
      new GlobalExceptionHandler()
          .handleBusinessException(
              new BusinessException(
                  ErrorCode.VALIDATION_FAILED, "nickname private-name", Map.of()));

      assertThat(logs.messages(Level.DEBUG))
          .containsExactly("business_error code=VALIDATION_FAILED status=400");
    }
  }

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
