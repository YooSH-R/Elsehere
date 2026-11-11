package com.elsehere.common.logging;

import java.util.Map;
import java.util.concurrent.Executor;
import org.slf4j.MDC;

/** 요청 로그 문맥(MDC의 요청 ID·사용자 ID)을 다른 스레드에서 도는 작업에도 이어 준다. 워커 로그도 같은 요청 ID로 찾을 수 있다. */
public final class LogContext {

  private LogContext() {}

  /** 지금 스레드의 로그 문맥을 담아 두었다가 {@code executor}에서 도는 작업마다 다시 건다. */
  public static Executor propagating(Executor executor) {
    Map<String, String> context = MDC.getCopyOfContextMap();
    return task ->
        executor.execute(
            () -> {
              if (context != null) {
                MDC.setContextMap(context);
              }
              try {
                task.run();
              } finally {
                MDC.clear();
              }
            });
  }
}
