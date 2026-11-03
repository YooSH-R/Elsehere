package com.elsehere.common.config;

import java.time.Instant;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;

/**
 * 테스트의 현재 시각을 {@link #NOW}로 고정한다. 실제 시계({@link ClockConfig}) 대신 불러온다.
 *
 * <p>Clock 빈은 같은 설정을 쓰는 테스트끼리 공유하므로, 시각을 옮긴 테스트는 끝나기 전에 {@link #NOW}로 되돌린다.
 */
@TestConfiguration(proxyBeanMethods = false)
public class TestClockConfig {

  /** 한국 시각 2026-11-02 10:00. 하루가 바뀌는 자정과 떨어져 있다. */
  public static final Instant NOW = Instant.parse("2026-11-02T01:00:00Z");

  @Bean
  public AdjustableClock clock() {
    return new AdjustableClock(NOW);
  }
}
