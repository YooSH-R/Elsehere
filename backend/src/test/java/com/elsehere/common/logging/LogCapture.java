package com.elsehere.common.logging;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import java.util.List;
import org.slf4j.LoggerFactory;

/**
 * 테스트에서 한 클래스가 남긴 로그를 모은다. try-with-resources로 쓰면 끝날 때 떼어 내고 레벨을 되돌린다.
 *
 * <p>운영 로그는 `이벤트명 키=값` 형식이라 메시지 문자열로 확인한다.
 */
public final class LogCapture implements AutoCloseable {

  private final Logger logger;
  private final Level previousLevel;
  private final ListAppender<ILoggingEvent> appender = new ListAppender<>();

  private LogCapture(Class<?> type, Level level) {
    logger = (Logger) LoggerFactory.getLogger(type);
    previousLevel = logger.getLevel();
    logger.setLevel(level);
    appender.start();
    logger.addAppender(appender);
  }

  /** 개발 서버처럼 DEBUG까지 모은다. */
  public static LogCapture atDebug(Class<?> type) {
    return new LogCapture(type, Level.DEBUG);
  }

  /** 운영처럼 INFO 이상만 모은다. */
  public static LogCapture atInfo(Class<?> type) {
    return new LogCapture(type, Level.INFO);
  }

  /** 그 레벨로 남긴 로그의 메시지(인자를 채운 문자열). */
  public List<String> messages(Level level) {
    return appender.list.stream()
        .filter(event -> event.getLevel() == level)
        .map(ILoggingEvent::getFormattedMessage)
        .toList();
  }

  @Override
  public void close() {
    logger.detachAppender(appender);
    appender.stop();
    logger.setLevel(previousLevel);
  }
}
