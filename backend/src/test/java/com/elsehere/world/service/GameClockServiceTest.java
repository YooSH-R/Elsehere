package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.AdjustableClock;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.DialogueTestData;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.dto.GameClockResponse;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
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
import org.springframework.jdbc.core.JdbcTemplate;

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

  @Autowired private JdbcTemplate jdbcTemplate;

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
  @DisplayName("튜토리얼을 마치기 전에는 시간이 멈추고, 튜토리얼을 마친 뒤 신호부터 흐른다")
  void beat_duringTutorial_pausesClock() {
    setOnboardingStep("TUTORIAL");
    gameClockService.beat(userId);
    clock.setInstant(NOW.plusSeconds(30));

    assertThat(gameClockService.beat(userId)).isEqualTo(new GameClockResponse(1, 480, true));

    setOnboardingStep("DONE");
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

  @Test
  @DisplayName("신호를 받으면 그날(한국 날짜) 접속을 남기고, 신호 사이에 지난 실제 시간을 머문 시간으로 더한다")
  void beat_recordsKoreanDayVisitWithStayedSeconds() {
    gameClockService.beat(userId);
    clock.setInstant(NOW.plusSeconds(30));
    gameClockService.beat(userId);

    // NOW는 한국 2026-10-30 10:00
    assertThat(visits()).containsExactly(new Visit("2026-10-30", NOW, NOW.plusSeconds(30), 30));
  }

  @Test
  @DisplayName("대화·학습 창이 열려 게임 시계가 멈춘 동안에도 화면에 머문 시간은 센다")
  void beat_whileInteractionPaused_stillCountsStayedSeconds() {
    gameClockService.beat(userId, true, false, 0L);
    clock.setInstant(NOW.plusSeconds(30));
    gameClockService.beat(userId, true, true, 0L);

    assertThat(visits()).extracting(Visit::activeSeconds).containsExactly(30L);
  }

  @Test
  @DisplayName("신호가 60초 넘게 끊겼다가 다시 오면(창을 닫았거나 숨김) 끊긴 사이는 머문 시간에 넣지 않는다")
  void beat_afterLongGap_addsNoStayedSeconds() {
    gameClockService.beat(userId);
    clock.setInstant(NOW.plusSeconds(600));
    gameClockService.beat(userId);

    assertThat(visits()).containsExactly(new Visit("2026-10-30", NOW, NOW.plusSeconds(600), 0));
  }

  @Test
  @DisplayName("한국 자정을 넘긴 신호는 다음 날 접속으로 남긴다")
  void beat_afterKoreanMidnight_recordsNextDay() {
    Instant beforeMidnight = Instant.parse("2026-10-30T14:59:50Z");
    Instant afterMidnight = beforeMidnight.plusSeconds(30);
    clock.setInstant(beforeMidnight);
    gameClockService.beat(userId);
    clock.setInstant(afterMidnight);
    gameClockService.beat(userId);

    assertThat(visits())
        .containsExactly(
            new Visit("2026-10-30", beforeMidnight, beforeMidnight, 0),
            new Visit("2026-10-31", afterMidnight, afterMidnight, 30));
  }

  private List<Visit> visits() {
    return jdbcTemplate.query(
        "select to_char(activity_date, 'YYYY-MM-DD'), first_seen_at, last_seen_at, active_seconds"
            + " from user_daily_activities where user_id = ? order by activity_date",
        (row, rowNumber) ->
            new Visit(
                row.getString(1),
                row.getTimestamp(2).toInstant(),
                row.getTimestamp(3).toInstant(),
                row.getLong(4)),
        userId);
  }

  private record Visit(String day, Instant firstSeenAt, Instant lastSeenAt, long activeSeconds) {}

  // 튜토리얼 완료 API(TUT-01)를 거치지 않고 온보딩 단계만 바꾼다.
  private void setOnboardingStep(String step) {
    entityManager.flush();
    entityManager
        .createNativeQuery("update users set onboarding_step = ? where id = ?")
        .setParameter(1, step)
        .setParameter(2, userId)
        .executeUpdate();
    entityManager.clear();
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    AdjustableClock clock() {
      return new AdjustableClock(NOW);
    }
  }
}
