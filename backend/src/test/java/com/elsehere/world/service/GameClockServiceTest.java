package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.DialogueTestData;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.dto.GameClockResponse;
import jakarta.persistence.EntityManager;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

// 신호 사이에 지난 실제 시간은 테스트 시계로 정한다. 대화는 시드의 NPC로 만든다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  GameClockService.class,
  GameClockServiceTest.ClockTestConfiguration.class
})
class GameClockServiceTest {

  private static final Instant NOW = Instant.parse("2026-10-30T01:00:00Z");

  @Autowired private GameClockService gameClockService;

  @Autowired private AdjustableClock clock;

  @Autowired private EntityManager entityManager;

  private Long userId;

  @BeforeEach
  void setUp() {
    clock.setInstant(NOW);
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
  }

  @Test
  @DisplayName("신호를 보낸 적 없는 사용자의 게임 시각은 1일차 오전 8시다")
  void now_withoutBeat_isFirstDayEightAm() {
    assertThat(gameClockService.now(userId)).isEqualTo(new GameClockResponse(1, 480, false));
  }

  @Test
  @DisplayName("첫 신호는 시계를 시작만 하고 시간을 더하지 않는다")
  void firstBeat_startsClockWithoutAdding() {
    assertThat(gameClockService.beat(userId)).isEqualTo(new GameClockResponse(1, 480, false));
  }

  @Test
  @DisplayName("신호 사이에 지난 실제 시간만큼 게임 시간이 흐른다")
  void beat_addsElapsedRealTime() {
    gameClockService.beat(userId);
    clock.setInstant(NOW.plusSeconds(30));

    GameClockResponse response = gameClockService.beat(userId);

    assertThat(response).isEqualTo(GameTime.ofPlaySeconds(30).response(false));
    assertThat(gameClockService.now(userId)).isEqualTo(response);
  }

  @Test
  @DisplayName("신호 사이가 길어도(창을 닫았거나 백그라운드) 한 번에 60초까지만 더한다")
  void beat_afterLongGap_addsAtMostSixtySeconds() {
    gameClockService.beat(userId);
    clock.setInstant(NOW.plusSeconds(5 * 60));

    assertThat(gameClockService.beat(userId)).isEqualTo(GameTime.ofPlaySeconds(60).response(false));
  }

  @Test
  @DisplayName("시계가 거꾸로 가면 시간을 더하지 않는다")
  void beat_clockGoesBack_addsNothing() {
    gameClockService.beat(userId);
    clock.setInstant(NOW.minusSeconds(10));

    assertThat(gameClockService.beat(userId)).isEqualTo(new GameClockResponse(1, 480, false));
  }

  @Test
  @DisplayName("진행 중인 대화가 있으면 시간이 멈추고, 대화가 끝난 뒤 신호부터 다시 흐른다")
  void beat_duringConversation_pausesClock() {
    gameClockService.beat(userId);
    var conversation = DialogueTestData.conversation(entityManager, userId, "CAFE_STAFF");
    clock.setInstant(NOW.plusSeconds(30));

    assertThat(gameClockService.beat(userId)).isEqualTo(new GameClockResponse(1, 480, true));

    conversation.endByUser(NOW.plusSeconds(40));
    clock.setInstant(NOW.plusSeconds(60));
    assertThat(gameClockService.beat(userId)).isEqualTo(GameTime.ofPlaySeconds(30).response(false));
  }

  @Test
  @DisplayName("게임 날짜는 사용자마다 따로 흐른다")
  void clocks_areSeparatePerUser() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");
    gameClockService.beat(userId);
    clock.setInstant(NOW.plusSeconds(30));
    gameClockService.beat(userId);

    assertThat(gameClockService.currentDay(userId)).isEqualTo(1);
    assertThat(gameClockService.now(otherUserId)).isEqualTo(new GameClockResponse(1, 480, false));
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    AdjustableClock clock() {
      return new AdjustableClock(NOW);
    }
  }

  /** 테스트 안에서 현재 시각을 바꿀 수 있는 Clock. 신호 사이에 지난 시간을 정할 때 쓴다. */
  static class AdjustableClock extends Clock {

    private Instant instant;

    AdjustableClock(Instant instant) {
      this.instant = instant;
    }

    void setInstant(Instant instant) {
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
}
