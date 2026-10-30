package com.elsehere.world.service;

import com.elsehere.world.dto.GameClockResponse;

/**
 * 누적 플레이 시간으로 정해지는 게임 시각. 실제 10분(600초)이 게임 하루이고, 새 사용자는 1일차 오전 8시에서 시작한다.
 *
 * @param day 게임 날짜. 1일차부터 센다
 * @param minuteOfDay 하루 중 분(0~1439)
 */
public record GameTime(int day, int minuteOfDay) {

  static final long REAL_SECONDS_PER_DAY = 600;
  private static final long MINUTES_PER_DAY = 24 * 60;
  // 오전 8시에 해당하는 플레이 초. 시작 시각을 바꾸려면 이 값만 바꾼다.
  private static final long START_SECONDS = REAL_SECONDS_PER_DAY * 8 / 24;

  public static GameTime ofPlaySeconds(long playSeconds) {
    long elapsed = START_SECONDS + playSeconds;
    int day = (int) (elapsed / REAL_SECONDS_PER_DAY) + 1;
    int minuteOfDay =
        (int) (elapsed % REAL_SECONDS_PER_DAY * MINUTES_PER_DAY / REAL_SECONDS_PER_DAY);
    return new GameTime(day, minuteOfDay);
  }

  public GameClockResponse response(boolean paused) {
    return new GameClockResponse(day, minuteOfDay, paused);
  }
}
