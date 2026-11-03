package com.elsehere.common.config;

import java.time.Clock;
import java.time.ZoneId;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/** 현재 시각이 필요한 코드는 이 {@link Clock}을 주입받는다. 테스트에서는 고정된 Clock으로 바꾼다. */
@Configuration(proxyBeanMethods = false)
public class ClockConfig {

  /** 서비스 시간대. 미션이 바뀌는 자정, 화면에 보이는 날짜, 사용량 집계 날짜, 정기 작업 시각의 기준이다. 시각 저장은 UTC로 한다. */
  public static final String SERVICE_ZONE_ID = "Asia/Seoul";

  public static final ZoneId SERVICE_ZONE = ZoneId.of(SERVICE_ZONE_ID);

  @Bean
  public Clock clock() {
    return Clock.systemUTC();
  }
}
