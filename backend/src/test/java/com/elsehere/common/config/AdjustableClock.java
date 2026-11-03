package com.elsehere.common.config;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;

/** 테스트 안에서 현재 시각을 정하고 바꿀 수 있는 Clock. 지난 시간이 결과를 바꾸는 테스트는 이 시각을 옮긴다. */
public class AdjustableClock extends Clock {

  private Instant instant;

  public AdjustableClock(Instant instant) {
    this.instant = instant;
  }

  public void setInstant(Instant instant) {
    this.instant = instant;
  }

  @Override
  public ZoneId getZone() {
    return ZoneOffset.UTC;
  }

  @Override
  public Clock withZone(ZoneId zone) {
    return Clock.fixed(instant, zone);
  }

  @Override
  public Instant instant() {
    return instant;
  }
}
