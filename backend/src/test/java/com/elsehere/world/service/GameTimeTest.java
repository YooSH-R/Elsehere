package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class GameTimeTest {

  @Test
  @DisplayName("플레이를 시작하지 않은 사용자는 1일차 오전 8시다")
  void noPlay_isFirstDayEightAm() {
    GameTime time = GameTime.ofPlaySeconds(0);

    assertThat(time.day()).isEqualTo(1);
    assertThat(time.minuteOfDay()).isEqualTo(8 * 60);
  }

  @ParameterizedTest(name = "플레이 {0}초 → {1}일차 {2}분")
  @CsvSource({
    // 실제 10분(600초)이 게임 하루다. 1초는 게임 2.4분이다.
    "30, 1, 552",
    "399, 1, 1437",
    "400, 2, 0",
    "1000, 3, 0",
    "1300, 3, 720"
  })
  @DisplayName("실제 10분이 게임 하루이고, 오전 8시에서 시작해 플레이한 만큼 흐른다")
  void playSeconds_advanceGameTime(long playSeconds, int day, int minuteOfDay) {
    GameTime time = GameTime.ofPlaySeconds(playSeconds);

    assertThat(time.day()).isEqualTo(day);
    assertThat(time.minuteOfDay()).isEqualTo(minuteOfDay);
  }
}
