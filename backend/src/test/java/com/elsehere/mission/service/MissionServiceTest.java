package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.mission.dto.MissionListResponse;
import com.elsehere.mission.dto.MissionResponse;
import com.elsehere.mission.entity.UserMissionStatus;
import com.elsehere.mission.repository.UserMissionRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.service.PronunciationAudioStore;
import jakarta.persistence.EntityManager;
import java.time.Clock;
import java.time.Instant;
import java.time.LocalDate;
import java.time.OffsetDateTime;
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

// 미션·NPC·장소는 시드(WorldSeedLoader가 시작 때 넣음)를 쓴다. 미션 날짜는 Asia/Seoul 기준이다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  MissionService.class,
  MissionServiceTest.ClockTestConfiguration.class
})
class MissionServiceTest {

  // 한국 시각 2026-10-27 10:00
  private static final Instant NOW = Instant.parse("2026-10-27T01:00:00Z");

  @Autowired private MissionService missionService;

  @Autowired private UserMissionRepository userMissionRepository;

  @Autowired private EntityManager entityManager;

  @Autowired private AdjustableClock clock;

  // Clock 빈은 테스트끼리 공유하므로 매번 기준 시각으로 되돌린다.
  @BeforeEach
  void resetClock() {
    clock.setInstant(NOW);
  }

  @Test
  @DisplayName("오늘 처음 조회하면 열린 장소의 일일 미션을 진행 중(RUN)으로 시작하고, 오늘 날짜와 다음 갱신 시각을 돌려준다")
  void firstQueryToday_startsDailyMissions() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    MissionListResponse response = missionService.getTodayMissions(userId);

    assertThat(response.date()).isEqualTo(LocalDate.parse("2026-10-27"));
    assertThat(response.resetsAt()).isEqualTo(OffsetDateTime.parse("2026-10-28T00:00:00+09:00"));
    assertThat(response.missions())
        .extracting(
            MissionResponse::missionCode,
            MissionResponse::title,
            MissionResponse::sceneCode,
            MissionResponse::npcCode,
            MissionResponse::npcName,
            MissionResponse::status,
            MissionResponse::completedAt)
        .containsExactlyInAnyOrder(
            tuple(
                "CAFE_SOY_LATTE",
                "나만의 카페라테 주문하기",
                "CAFE",
                "CAFE_STAFF",
                "Mia",
                UserMissionStatus.RUN,
                null),
            tuple(
                "HOTEL_CHECK_IN",
                "호텔에 체크인하기",
                "HOTEL",
                "HOTEL_STAFF",
                "Emma",
                UserMissionStatus.RUN,
                null));
    assertThat(response.missions())
        .allSatisfy(
            mission -> {
              assertThat(mission.missionId()).isNotNull();
              assertThat(mission.userMissionId()).isNotNull();
              assertThat(mission.description()).isNotBlank();
            });
  }

  @Test
  @DisplayName("같은 날 다시 조회하면 새로 만들지 않고 처음 시작한 미션을 그대로 돌려준다")
  void queryTwiceSameDay_doesNotStartAgain() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    MissionListResponse first = missionService.getTodayMissions(userId);

    MissionListResponse second = missionService.getTodayMissions(userId);

    assertThat(second.missions())
        .extracting(MissionResponse::userMissionId)
        .containsExactlyInAnyOrderElementsOf(
            first.missions().stream().map(MissionResponse::userMissionId).toList());
    assertThat(userMissionRepository.count()).isEqualTo(2);
  }

  @Test
  @DisplayName("한국 시각 자정이 지나면 날짜가 바뀌고, 전날 완료한 미션도 새 날짜로 다시 시작한다")
  void afterMidnightInSeoul_startsNextDayMissions() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    // 한국 시각 2026-10-26 23:00. 카페 미션을 끝낸 상태로 둔다.
    clock.setInstant(Instant.parse("2026-10-26T14:00:00Z"));
    missionService.getTodayMissions(userId);
    completeMission(userId, "CAFE_SOY_LATTE");

    // 한국 시각 2026-10-27 00:30. UTC로는 아직 2026-10-26이다.
    clock.setInstant(Instant.parse("2026-10-26T15:30:00Z"));
    MissionListResponse response = missionService.getTodayMissions(userId);

    assertThat(response.date()).isEqualTo(LocalDate.parse("2026-10-27"));
    assertThat(response.resetsAt()).isEqualTo(OffsetDateTime.parse("2026-10-28T00:00:00+09:00"));
    assertThat(response.missions())
        .extracting(MissionResponse::missionCode, MissionResponse::status)
        .containsExactlyInAnyOrder(
            tuple("CAFE_SOY_LATTE", UserMissionStatus.RUN),
            tuple("HOTEL_CHECK_IN", UserMissionStatus.RUN));
    assertThat(userMissionRepository.count()).isEqualTo(4);
  }

  @Test
  @DisplayName("열리지 않은 장소의 미션은 시작하지 않고 목록에도 넣지 않는다")
  void closedSceneMissions_areExcluded() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    // MVP 시드는 모든 장소가 열려 있어 테스트에서 호텔을 닫는다.
    entityManager
        .createNativeQuery("update scenes set is_open = false where code = 'HOTEL'")
        .executeUpdate();

    MissionListResponse response = missionService.getTodayMissions(userId);

    assertThat(response.missions())
        .extracting(MissionResponse::missionCode)
        .containsExactly("CAFE_SOY_LATTE");
    assertThat(userMissionRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("완료한 미션은 DONE과 완료 시각(한국 시각)으로 보여주고, 나머지는 진행 중으로 둔다")
  void completedMission_showsDoneAndCompletedAt() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    missionService.getTodayMissions(userId);
    completeMission(userId, "CAFE_SOY_LATTE");

    MissionListResponse response = missionService.getTodayMissions(userId);

    assertThat(response.missions())
        .extracting(
            MissionResponse::missionCode, MissionResponse::status, MissionResponse::completedAt)
        .containsExactlyInAnyOrder(
            tuple(
                "CAFE_SOY_LATTE",
                UserMissionStatus.DONE,
                OffsetDateTime.parse("2026-10-27T10:00:00+09:00")),
            tuple("HOTEL_CHECK_IN", UserMissionStatus.RUN, null));
  }

  @Test
  @DisplayName("다른 사용자가 시작하거나 완료한 미션은 내 미션 목록에 섞이지 않는다")
  void otherUsersMissions_areSeparate() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    MissionListResponse others = missionService.getTodayMissions(otherUserId);
    completeMission(otherUserId, "CAFE_SOY_LATTE");

    MissionListResponse mine = missionService.getTodayMissions(userId);

    assertThat(mine.missions())
        .allSatisfy(mission -> assertThat(mission.status()).isEqualTo(UserMissionStatus.RUN));
    assertThat(mine.missions())
        .extracting(MissionResponse::userMissionId)
        .doesNotContainAnyElementsOf(
            others.missions().stream().map(MissionResponse::userMissionId).toList());
    assertThat(userMissionRepository.count()).isEqualTo(4);
  }

  // 미션 완료 판정(대화) 전이라 SQL로 완료 상태를 만든다.
  private void completeMission(Long userId, String missionCode) {
    entityManager
        .createNativeQuery(
            "update user_missions set status = 'DONE', completed_at = ?"
                + " where user_id = ? and mission_id = (select id from missions where code = ?)")
        .setParameter(1, clock.instant())
        .setParameter(2, userId)
        .setParameter(3, missionCode)
        .executeUpdate();
    // SQL로 바꾼 값을 다음 조회가 읽도록 이미 불러온 엔티티를 비운다.
    entityManager.clear();
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    AdjustableClock clock() {
      return new AdjustableClock(NOW);
    }
  }

  /** 테스트 안에서 현재 시각을 바꿀 수 있는 Clock. 날짜가 바뀌는 경우를 검증할 때 쓴다. */
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
