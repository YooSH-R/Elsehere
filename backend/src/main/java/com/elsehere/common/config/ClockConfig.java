package com.elsehere.common.config;

import java.time.Clock;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/** 현재 시각이 필요한 코드는 이 {@link Clock}을 주입받는다. 테스트에서는 고정된 Clock으로 바꾼다. */
@Configuration(proxyBeanMethods = false)
public class ClockConfig {

  @Bean
  public Clock clock() {
    return Clock.systemUTC();
  }
}
