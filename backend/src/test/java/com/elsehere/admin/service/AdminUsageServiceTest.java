package com.elsehere.admin.service;

import static java.time.temporal.ChronoUnit.DAYS;
import static java.time.temporal.ChronoUnit.HOURS;
import static java.time.temporal.ChronoUnit.MINUTES;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.admin.dto.OverallUsageReport;
import com.elsehere.admin.dto.UsageReport;
import com.elsehere.admin.dto.UserUsageReport;
import com.elsehere.admin.repository.AdminUsageRepository;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.service.PronunciationAudioStore;
import jakarta.persistence.EntityManager;
import java.sql.Timestamp;
import java.time.Clock;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.jdbc.core.JdbcTemplate;

// 활동 기록은 실제 PostgreSQL에 SQL로 넣는다. NPC·어휘·미션은 시드를 쓴다. 다른 기록과 섞이지 않도록 먼 미래 시각을 쓰고, 사용자 생성
// 시각은 DB 기본값(실제 현재 시각)이라 따로 정하지 않으면 기간 밖이다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  AdminUsageRepository.class,
  AdminUsageService.class,
  AdminUsageServiceTest.ClockTestConfiguration.class
})
class AdminUsageServiceTest {

  // 한국 시각 2030-01-10 09:00
  private static final Instant NOW = Instant.parse("2030-01-10T00:00:00Z");

  private final AtomicInteger nextVocab = new AtomicInteger();

  private final AtomicInteger nextMissionDay = new AtomicInteger();

  @Autowired private AdminUsageService adminUsageService;

  @Autowired private EntityManager entityManager;

  @Autowired private JdbcTemplate jdbcTemplate;

  @Test
  @DisplayName("활동 흔적이 있는 날을 한국 날짜별로 게스트·가입자로 나눠 센다. 기간 밖과 예약만 된 AI 호출은 활동이 아니다")
  void report_countsActiveUsersByKoreanDay() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    Long returningGuest = user(AuthProvider.GUEST, "guest-uid-2", "guest-000002");
    Long idle = user(AuthProvider.GOOGLE, "google-sub-2", "Moon");
    // 한국 시각 2030-01-09 23:59와 2030-01-10 00:00
    conversation(member, "CAFE_STAFF", "ACTIVE", null, Instant.parse("2030-01-09T14:59:00Z"));
    aiCall(member, "NPC", "OK", Instant.parse("2030-01-09T15:00:00Z"));
    createdAt(guest, NOW.minus(1, HOURS));
    wordSave(guest, "OBJECT", NOW.minus(2, HOURS));
    gameClock(returningGuest, 600, NOW.minus(1, DAYS));
    missionDone(returningGuest, NOW.minus(30, MINUTES));
    aiCall(idle, "NPC", "RESERVED", NOW.minus(1, HOURS));
    conversation(idle, "CAFE_STAFF", "ACTIVE", null, NOW.minus(8, DAYS));

    OverallUsageReport report = adminUsageService.report(7);

    assertThat(report.usage().byDay())
        .containsExactly(
            new UsageReport.Day("2030-01-09", 2, 1, 1, 0, 0, 1, 0, 0, 0),
            new UsageReport.Day("2030-01-10", 3, 2, 1, 1, 0, 0, 1, 1, 0));
  }

  @Test
  @DisplayName("기간 합계는 활동 사용자와 활동한 날 수를 세고, 서로 다른 이틀 이상 활동한 사용자를 다시 온 사용자로 센다")
  void report_summarizesActiveAndReturningUsers() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    Long idle = user(AuthProvider.GOOGLE, "google-sub-2", "Moon");
    conversation(member, "CAFE_STAFF", "ACTIVE", null, NOW.minus(2, DAYS));
    aiCall(member, "NPC", "OK", NOW.minus(1, HOURS));
    aiCall(member, "NPC", "OK", NOW.minus(2, HOURS));
    createdAt(guest, NOW.minus(1, HOURS));
    gameClock(guest, 600, NOW.minus(30, MINUTES));
    gameClock(idle, 1200, NOW.minus(8, DAYS));

    UsageReport.Summary summary = adminUsageService.report(7).usage().summary();

    assertThat(summary)
        .extracting(
            UsageReport.Summary::activeUsers,
            UsageReport.Summary::activeGuests,
            UsageReport.Summary::activeMembers,
            UsageReport.Summary::activeUserDays,
            UsageReport.Summary::returningUsers,
            UsageReport.Summary::newUsers,
            UsageReport.Summary::playSeconds)
        .containsExactly(2L, 1L, 1L, 3L, 1L, 1L, 600L);
  }

  @Test
  @DisplayName("게스트는 약관에 동의하지 않으므로, 첫 약관 동의가 계정 생성보다 1분 넘게 늦은 회원을 그 동의 날짜에 가입 전환으로 센다")
  void report_countsGuestConversionsByFirstTermsConsent() {
    Long converted = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    createdAt(converted, NOW.minus(3, DAYS));
    consent(converted, "TERMS", NOW.minus(1, HOURS));
    consent(converted, "TERMS", NOW.minus(30, MINUTES));
    Long directMember = user(AuthProvider.GOOGLE, "google-sub-2", "Moon");
    createdAt(directMember, NOW.minus(1, HOURS));
    consent(directMember, "TERMS", NOW.minus(1, HOURS).plusSeconds(2));
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    createdAt(guest, NOW.minus(2, HOURS));
    consent(guest, "VOICE", NOW.minus(1, HOURS));
    Long convertedBefore = user(AuthProvider.GOOGLE, "google-sub-3", "Star");
    createdAt(convertedBefore, NOW.minus(20, DAYS));
    consent(convertedBefore, "TERMS", NOW.minus(10, DAYS));

    UsageReport usage = adminUsageService.report(7).usage();

    assertThat(usage.summary().conversions()).isEqualTo(1);
    assertThat(usage.byDay())
        .extracting(UsageReport.Day::day, UsageReport.Day::conversions)
        .containsExactly(tuple("2030-01-07", 0L), tuple("2030-01-10", 1L));
  }

  @Test
  @DisplayName("기간 안에 시작한 대화를 끝난 방식별·NPC별로 센다. 끝내지 않은 대화는 ACTIVE로 묶는다")
  void report_countsConversationsByEndAndNpc() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    conversation(member, "CAFE_STAFF", "ENDED_BY_NPC", "NPC_ENDED", NOW.minus(1, HOURS));
    conversation(member, "CAFE_STAFF", "ENDED_BY_NPC", "NPC_ENDED", NOW.minus(2, HOURS));
    conversation(member, "CAFE_STAFF", "ACTIVE", null, NOW.minus(3, HOURS));
    conversation(member, "HOTEL_STAFF", "ENDED_BY_USER", "USER_ENDED", NOW.minus(1, DAYS));
    conversation(guest, "CAFE_STAFF", "ENDED_BY_NPC", "RUDE", NOW.minus(3, HOURS));
    conversation(guest, "CAFE_STAFF", "ENDED_BY_NPC", "NPC_ENDED", NOW.minus(8, DAYS));

    UsageReport usage = adminUsageService.report(7).usage();

    assertThat(usage.summary())
        .extracting(
            UsageReport.Summary::conversations,
            UsageReport.Summary::endedConversations,
            UsageReport.Summary::conversingUsers)
        .containsExactly(5L, 4L, 2L);
    assertThat(usage.conversationEnds())
        .containsExactly(
            new UsageReport.Count("NPC_ENDED", 2, 1),
            new UsageReport.Count("ACTIVE", 1, 1),
            new UsageReport.Count("RUDE", 1, 1),
            new UsageReport.Count("USER_ENDED", 1, 1));
    assertThat(usage.conversationsByNpc())
        .containsExactly(
            new UsageReport.Count("Mia · 카페 직원", 4, 2),
            new UsageReport.Count("Jake · 호텔 직원", 1, 1));
  }

  @Test
  @DisplayName("대화 중 행동은 학습자가 음성·글로 보낸 말과 힌트·뜻 보기 요청을 센다. NPC 말과 예약만 된 요청은 뺀다")
  void report_countsConversationActions() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    Long memberConversation =
        conversation(member, "CAFE_STAFF", "ACTIVE", null, NOW.minus(1, HOURS));
    turn(memberConversation, 1, "NPC", null, NOW.minus(1, HOURS));
    turn(memberConversation, 2, "USER", "VOICE", NOW.minus(1, HOURS));
    turn(memberConversation, 3, "USER", "TEXT", NOW.minus(1, HOURS));
    turn(memberConversation, 4, "USER", "TEXT", NOW.minus(1, HOURS));
    turn(memberConversation, 5, "USER", "TEXT", NOW.minus(1, HOURS));
    aiCall(member, "HINT", "OK", NOW.minus(1, HOURS));
    aiCall(member, "HINT", "FAILED", NOW.minus(1, HOURS));
    aiCall(member, "HINT", "RESERVED", NOW.minus(1, HOURS));
    aiCall(member, "MEANING", "OK", NOW.minus(1, HOURS));
    aiCall(member, "NPC", "OK", NOW.minus(1, HOURS));
    Long guestConversation = conversation(guest, "CAFE_STAFF", "ACTIVE", null, NOW.minus(2, HOURS));
    turn(guestConversation, 1, "USER", "VOICE", NOW.minus(2, HOURS));
    aiCall(guest, "MEANING", "OK", NOW.minus(2, HOURS));
    Long oldConversation = conversation(guest, "CAFE_STAFF", "ACTIVE", null, NOW.minus(8, DAYS));
    turn(oldConversation, 1, "USER", "TEXT", NOW.minus(8, DAYS));

    UsageReport usage = adminUsageService.report(7).usage();

    assertThat(usage.conversationActions())
        .containsExactly(
            new UsageReport.Count("TEXT", 3, 1),
            new UsageReport.Count("HINT", 2, 1),
            new UsageReport.Count("MEANING", 2, 2),
            new UsageReport.Count("VOICE", 2, 2));
  }

  @Test
  @DisplayName("기간 안에 완료한 미션과 저장한 단어를 세고, 단어는 저장한 곳별로 나눈다")
  void report_countsMissionsAndWordSavesBySource() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    wordSave(member, "OBJECT", NOW.minus(1, HOURS));
    wordSave(member, "NPC_LINE", NOW.minus(1, HOURS));
    wordSave(member, "NPC_LINE", NOW.minus(2, HOURS));
    wordSave(guest, "NPC_LINE", NOW.minus(1, HOURS));
    wordSave(guest, "FEEDBACK", NOW.minus(8, DAYS));
    missionDone(member, NOW.minus(1, HOURS));
    missionDone(member, NOW.minus(9, DAYS));

    UsageReport usage = adminUsageService.report(7).usage();

    assertThat(usage.summary())
        .extracting(UsageReport.Summary::missionsCompleted, UsageReport.Summary::wordsSaved)
        .containsExactly(1L, 4L);
    assertThat(usage.wordSaves())
        .containsExactly(
            new UsageReport.Count("NPC_LINE", 3, 2), new UsageReport.Count("OBJECT", 1, 1));
  }

  @Test
  @DisplayName("어드민이 실행한 발음 만들기처럼 사용자가 없는 AI 호출은 활동 사용자나 진행 단계에 세지 않는다")
  void report_ignoresAiCallsWithoutUser() {
    aiCall(null, "TTS", "OK", NOW.minus(1, HOURS));

    OverallUsageReport report = adminUsageService.report(7);

    assertThat(report.usage().summary().activeUsers()).isZero();
    assertThat(report.progress().firstDayStages())
        .first()
        .isEqualTo(new OverallUsageReport.UserCount("시작 전", 0));
  }

  @Test
  @DisplayName("대화 피드백을 결과별로 세고, 사용자가 건너뛴 피드백은 평가할 말이 없어 건너뛴 것과 나눈다")
  void report_countsFeedbacksByResult() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    feedback(member, "DONE", null, NOW.minus(1, HOURS));
    feedback(member, "SKIPPED", null, NOW.minus(1, HOURS));
    feedback(member, "SKIPPED", "NO_USABLE_USER_TURNS", NOW.minus(1, HOURS));
    feedback(guest, "DONE", null, NOW.minus(2, HOURS));
    feedback(guest, "FAILED", "AI_TIMEOUT", NOW.minus(2, HOURS));
    feedback(guest, "DONE", null, NOW.minus(8, DAYS));

    UsageReport usage = adminUsageService.report(7).usage();

    assertThat(usage.feedbacks())
        .containsExactly(
            new UsageReport.Count("DONE", 2, 2),
            new UsageReport.Count("FAILED", 1, 1),
            new UsageReport.Count("SKIPPED", 1, 1),
            new UsageReport.Count("SKIPPED_BY_USER", 1, 1));
  }

  @Test
  @DisplayName("새 사용자는 초기 설정 단계별로, 활동 사용자는 첫날 미션·마을 여정의 지금 단계별로 센다. 아무도 없는 단계도 0명으로 보여 준다")
  void report_countsUsersByProgressStage() {
    Long tutorialGuest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    createdAt(tutorialGuest, NOW.minus(1, HOURS));
    onboardingStep(tutorialGuest, "TUTORIAL");
    firstDayStage(tutorialGuest, 0);
    Long finishedGuest = user(AuthProvider.GUEST, "guest-uid-2", "guest-000002");
    createdAt(finishedGuest, NOW.minus(2, HOURS));
    firstDayStage(finishedGuest, 7);
    townStoryStage(finishedGuest, 3);
    Long newMember = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    createdAt(newMember, NOW.minus(1, HOURS));
    firstDayStage(newMember, 0);
    Long oldMember = user(AuthProvider.GOOGLE, "google-sub-2", "Moon");
    onboardingStep(oldMember, "LANG");
    aiCall(oldMember, "NPC", "OK", NOW.minus(1, HOURS));
    Long idle = user(AuthProvider.GOOGLE, "google-sub-3", "Star");
    firstDayStage(idle, 2);

    OverallUsageReport.Progress progress = adminUsageService.report(7).progress();

    assertThat(progress.onboardingSteps())
        .containsExactly(
            new OverallUsageReport.UserCount("LANG", 0),
            new OverallUsageReport.UserCount("LEVEL", 0),
            new OverallUsageReport.UserCount("TUTORIAL", 1),
            new OverallUsageReport.UserCount("DONE", 2));
    assertThat(progress.firstDayStages())
        .hasSize(9)
        .startsWith(
            new OverallUsageReport.UserCount("시작 전", 1),
            new OverallUsageReport.UserCount("1. 호텔에 도착했어요", 2))
        .endsWith(new OverallUsageReport.UserCount("모두 마침", 1));
    assertThat(progress.townStoryStages())
        .hasSize(14)
        .contains(
            new OverallUsageReport.UserCount("시작 전", 3),
            new OverallUsageReport.UserCount("4. WASH", 1));
  }

  @Test
  @DisplayName("사용자 목록은 기간 안에 활동한 사용자를 마지막 활동(한국 시각)이 늦은 순서로 보여 주고, 가입자는 이메일로, 게스트는 '게스트 #ID'로 부른다")
  void report_listsActiveUsersByLastActivity() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    email(member, "sun@example.com");
    conversation(member, "CAFE_STAFF", "ACTIVE", null, NOW.minus(2, DAYS));
    aiCall(member, "NPC", "OK", NOW.minus(3, HOURS));
    gameClock(member, 900, NOW.minus(5, HOURS));
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    createdAt(guest, NOW.minus(1, HOURS));
    missionDone(guest, NOW.minus(30, MINUTES));
    user(AuthProvider.GOOGLE, "google-sub-2", "Moon");

    OverallUsageReport report = adminUsageService.report(7);

    assertThat(report.users())
        .containsExactly(
            new OverallUsageReport.UserActivity(
                guest, true, "게스트 #" + guest, 1, 0, 1, 0, "2030-01-10 08:30"),
            new OverallUsageReport.UserActivity(
                member, false, "sun@example.com", 2, 1, 0, 900, "2030-01-10 06:00"));
  }

  @Test
  @DisplayName("사용자별 이용 현황은 그 사용자의 활동만 센다")
  void userReport_countsOnlyThatUser() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long other = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    conversation(member, "CAFE_STAFF", "ENDED_BY_NPC", "NPC_ENDED", NOW.minus(1, DAYS));
    wordSave(member, "NPC_LINE", NOW.minus(1, HOURS));
    aiCall(member, "HINT", "OK", NOW.minus(1, HOURS));
    conversation(other, "HOTEL_STAFF", "ACTIVE", null, NOW.minus(1, HOURS));
    wordSave(other, "OBJECT", NOW.minus(1, HOURS));
    aiCall(other, "MEANING", "OK", NOW.minus(1, HOURS));

    UsageReport usage = adminUsageService.userReport(member, 7).usage();

    assertThat(usage.summary())
        .extracting(
            UsageReport.Summary::activeUsers,
            UsageReport.Summary::activeUserDays,
            UsageReport.Summary::conversations,
            UsageReport.Summary::wordsSaved)
        .containsExactly(1L, 2L, 1L, 1L);
    assertThat(usage.byDay())
        .extracting(UsageReport.Day::day)
        .containsExactly("2030-01-09", "2030-01-10");
    assertThat(usage.conversationEnds()).containsExactly(new UsageReport.Count("NPC_ENDED", 1, 1));
    assertThat(usage.conversationsByNpc())
        .containsExactly(new UsageReport.Count("Mia · 카페 직원", 1, 1));
    assertThat(usage.conversationActions()).containsExactly(new UsageReport.Count("HINT", 1, 1));
    assertThat(usage.wordSaves()).containsExactly(new UsageReport.Count("NPC_LINE", 1, 1));
  }

  @Test
  @DisplayName(
      "사용자 개요는 이름(가입자는 이메일, 탈퇴는 '탈퇴 #ID')·구분·시작 시각·가입 전환 시각·진행 단계·누적 플레이 시간을 한국 시각으로 보여 준다")
  void userReport_showsProfile() {
    Long converted = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    email(converted, "sun@example.com");
    createdAt(converted, NOW.minus(3, DAYS));
    consent(converted, "TERMS", NOW.minus(1, HOURS));
    firstDayStage(converted, 7);
    townStoryStage(converted, 0);
    gameClock(converted, 1800, NOW.minus(10, MINUTES));
    Long withdrawnGuest = user(AuthProvider.GUEST, "withdrawn-uid-1", "withdrawn-2");
    createdAt(withdrawnGuest, NOW.minus(2, DAYS));
    onboardingStep(withdrawnGuest, "TUTORIAL");

    assertThat(adminUsageService.userReport(converted, 7).profile())
        .isEqualTo(
            new UserUsageReport.Profile(
                converted,
                false,
                "sun@example.com",
                false,
                "2030-01-07 09:00",
                "2030-01-10 08:00",
                "DONE",
                "모두 마침",
                "1. DINER",
                1800,
                "2030-01-10 08:50"));
    assertThat(adminUsageService.userReport(withdrawnGuest, 7).profile())
        .isEqualTo(
            new UserUsageReport.Profile(
                withdrawnGuest,
                true,
                "탈퇴 #" + withdrawnGuest,
                true,
                "2030-01-08 09:00",
                null,
                "TUTORIAL",
                "시작 전",
                "시작 전",
                0,
                null));
  }

  @Test
  @DisplayName("팀 기기에서 쓴 팀 계정은 전체 이용 현황의 모든 집계에서 뺀다")
  void report_leavesOutTeamAccounts() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long team = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    teamAccount(team);
    for (Long userId : List.of(member, team)) {
      createdAt(userId, NOW.minus(2, DAYS));
      firstDayStage(userId, 0);
      Long conversationId =
          conversation(userId, "CAFE_STAFF", "ENDED_BY_NPC", "NPC_ENDED", NOW.minus(1, HOURS));
      turn(conversationId, 1, "USER", "TEXT", NOW.minus(1, HOURS));
      aiCall(userId, "HINT", "OK", NOW.minus(1, HOURS));
      wordSave(userId, "OBJECT", NOW.minus(1, HOURS));
      feedback(userId, "DONE", null, NOW.minus(1, HOURS));
      visit(userId, NOW.minus(1, DAYS), 300);
    }

    OverallUsageReport report = adminUsageService.report(7);

    UsageReport usage = report.usage();
    assertThat(usage.summary())
        .extracting(
            UsageReport.Summary::activeUsers,
            UsageReport.Summary::newUsers,
            UsageReport.Summary::conversingUsers,
            UsageReport.Summary::wordsSaved,
            UsageReport.Summary::activeSeconds)
        .containsExactly(1L, 1L, 1L, 1L, 300L);
    assertThat(usage.byDay()).extracting(UsageReport.Day::activeUsers).containsOnly(1L);
    for (List<UsageReport.Count> counts :
        List.of(
            usage.conversationEnds(),
            usage.conversationsByNpc(),
            usage.conversationActions(),
            usage.wordSaves(),
            usage.feedbacks())) {
      assertThat(counts).isNotEmpty().extracting(UsageReport.Count::users).containsOnly(1L);
    }
    assertThat(report.progress().onboardingSteps())
        .extracting(OverallUsageReport.UserCount::users)
        .containsExactly(0L, 0L, 0L, 1L);
    assertThat(report.progress().firstDayStages())
        .first()
        .isEqualTo(new OverallUsageReport.UserCount("시작 전", 0));
    assertThat(report.progress().firstDayStages())
        .contains(new OverallUsageReport.UserCount("1. 호텔에 도착했어요", 1));
    assertThat(report.retention().startedUsers()).isEqualTo(1);
    assertThat(report.users())
        .extracting(OverallUsageReport.UserActivity::userId)
        .containsExactly(member);
  }

  @Test
  @DisplayName("팀 계정이어도 그 사용자를 골라 보면 그 사용자의 활동을 센다")
  void userReport_countsTeamAccountWhenChosen() {
    Long team = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    teamAccount(team);
    conversation(team, "CAFE_STAFF", "ACTIVE", null, NOW.minus(1, HOURS));
    wordSave(team, "OBJECT", NOW.minus(1, HOURS));

    UsageReport usage = adminUsageService.userReport(team, 7).usage();

    assertThat(usage.summary())
        .extracting(
            UsageReport.Summary::activeUsers,
            UsageReport.Summary::conversations,
            UsageReport.Summary::wordsSaved)
        .containsExactly(1L, 1L, 1L);
  }

  @Test
  @DisplayName("사용자 찾기는 가입 이메일(앞뒤 공백·대소문자 무시)이나 사용자 ID로 찾고, 없는 이메일은 NOT_FOUND로 거절한다")
  void findUserId_byEmailOrId() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    email(member, "Sun@Example.com");

    assertThat(adminUsageService.findUserId(" sun@example.com ")).isEqualTo(member);
    assertThat(adminUsageService.findUserId("57")).isEqualTo(57L);
    assertThatThrownBy(() -> adminUsageService.findUserId("moon@example.com"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("없는 사용자 ID의 이용 현황은 NOT_FOUND로 거절한다")
  void userReport_unknownUser_isRejected() {
    assertThatThrownBy(() -> adminUsageService.userReport(999_999L, 7))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("일별 접속 기록만 있는 날도 활동한 날로 세고, 날짜별과 기간 전체의 머문 시간을 더한다")
  void report_countsVisitDaysAndActiveTime() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    // 한국 2030-01-09 09:00까지 10분, 2030-01-10 08:00까지 5분 머묾
    visit(member, NOW.minus(1, DAYS), 600);
    visit(member, NOW.minus(1, HOURS), 300);

    UsageReport usage = adminUsageService.report(7).usage();

    assertThat(usage.byDay())
        .extracting(
            UsageReport.Day::day, UsageReport.Day::activeUsers, UsageReport.Day::activeSeconds)
        .containsExactly(tuple("2030-01-09", 1L, 600L), tuple("2030-01-10", 1L, 300L));
    assertThat(usage.summary())
        .extracting(
            UsageReport.Summary::activeUsers,
            UsageReport.Summary::activeUserDays,
            UsageReport.Summary::returningUsers,
            UsageReport.Summary::activeSeconds)
        .containsExactly(1L, 2L, 1L, 900L);
    assertThat(usage.summary().averageDailyActiveMinutes()).isEqualTo(7.5);
  }

  @Test
  @DisplayName("재방문은 기간 안에 계정을 만든 사용자를 만든 날부터 세고, 확인할 날이 다 지난 사용자만 D1·D7·7일 안 재방문 대상으로 삼는다")
  void report_countsRetentionOfStartedUsers() {
    // 오늘은 한국 2030-01-10이다.
    Long both = startedOn("2030-01-01", "google-sub-1");
    Long midWeek = startedOn("2030-01-01", "google-sub-2");
    startedOn("2030-01-01", "google-sub-3");
    Long yesterdayReturned = startedOn("2030-01-08", "google-sub-4");
    Long tooRecent = startedOn("2030-01-09", "google-sub-5");
    Long beforePeriod = startedOn("2029-12-01", "google-sub-6");
    visit(both, koreanTenAm("2030-01-02"), 300);
    visit(both, koreanTenAm("2030-01-08"), 300);
    conversation(midWeek, "CAFE_STAFF", "ACTIVE", null, koreanTenAm("2030-01-05"));
    visit(yesterdayReturned, koreanTenAm("2030-01-09"), 300);
    visit(tooRecent, NOW, 300);
    visit(beforePeriod, koreanTenAm("2029-12-02"), 300);

    OverallUsageReport.Retention retention = adminUsageService.report(30).retention();

    // 기간 안에 만든 사용자 5명. D1 대상은 다음 날이 다 지난 4명이고 그중 2명이 다음 날 왔다.
    // D7·7일 안 대상은 1월 1일에 만든 3명이고, 7일째에 1명, 1~7일째 중 하루라도 2명이 왔다.
    assertThat(retention).isEqualTo(new OverallUsageReport.Retention(5, 4, 2, 3, 1, 2));
    assertThat(retention.day1Rate()).isEqualTo(0.5);
  }

  @ParameterizedTest
  @ValueSource(ints = {0, -1, 91})
  @DisplayName("기간이 1~90일을 벗어나면 전체·사용자별 이용 현황 모두 VALIDATION_FAILED로 거절한다")
  void report_daysOutOfRange_isRejected(int days) {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");

    assertThatThrownBy(() -> adminUsageService.report(days))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
    assertThatThrownBy(() -> adminUsageService.userReport(member, days))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
  }

  private Long user(AuthProvider provider, String providerUid, String nickname) {
    Long userId = UserTestData.insertUser(entityManager, provider, providerUid, nickname);
    entityManager.flush();
    return userId;
  }

  private void email(Long userId, String email) {
    jdbcTemplate.update("update users set email = ? where id = ?", email, userId);
  }

  private void teamAccount(Long userId) {
    jdbcTemplate.update("update users set team_account = true where id = ?", userId);
  }

  private void createdAt(Long userId, Instant at) {
    jdbcTemplate.update("update users set created_at = ? where id = ?", Timestamp.from(at), userId);
  }

  private void onboardingStep(Long userId, String step) {
    jdbcTemplate.update("update users set onboarding_step = ? where id = ?", step, userId);
  }

  private void firstDayStage(Long userId, int stage) {
    jdbcTemplate.update(
        "insert into first_day_missions (user_id, stage, objectives, scene, version)"
            + " values (?, ?, 0, 'HOTEL', 0)",
        userId,
        stage);
  }

  private void townStoryStage(Long userId, int stage) {
    jdbcTemplate.update(
        "insert into town_stories (user_id, stage, completed_goals, version) values (?, ?, 0, 0)",
        userId,
        stage);
  }

  private void consent(Long userId, String consentType, Instant agreedAt) {
    jdbcTemplate.update(
        "insert into user_consents (user_id, consent_type, version, agreed_at) values (?, ?,"
            + " '2026-10-01', ?)",
        userId,
        consentType,
        Timestamp.from(agreedAt));
  }

  private Long conversation(
      Long userId, String npcCode, String status, String endReason, Instant startedAt) {
    return jdbcTemplate.queryForObject(
        "insert into conversations (user_id, npc_id, initiated_by, level_applied, status,"
            + " end_reason, started_at) values (?, (select id from npcs where code = ?), 'USER', 1,"
            + " ?, ?, ?) returning id",
        Long.class,
        userId,
        npcCode,
        status,
        endReason,
        Timestamp.from(startedAt));
  }

  private void turn(
      Long conversationId, int seq, String speaker, String inputType, Instant createdAt) {
    jdbcTemplate.update(
        "insert into turns (conversation_id, seq, speaker, input_type, text, created_at,"
            + " updated_at) values (?, ?, ?, ?, 'Hello.', ?, ?)",
        conversationId,
        seq,
        speaker,
        inputType,
        Timestamp.from(createdAt),
        Timestamp.from(createdAt));
  }

  // 끝낸 대화 하나와 그 피드백
  private void feedback(Long userId, String status, String errorCode, Instant at) {
    Long conversationId = conversation(userId, "CAFE_STAFF", "ENDED_BY_USER", "USER_ENDED", at);
    jdbcTemplate.update(
        "insert into conversation_feedback (conversation_id, status, error_code, created_at,"
            + " updated_at) values (?, ?, ?, ?, ?)",
        conversationId,
        status,
        errorCode,
        Timestamp.from(at),
        Timestamp.from(at));
  }

  private void aiCall(Long userId, String kind, String status, Instant at) {
    jdbcTemplate.update(
        "insert into ai_requests (request_id, kind, status, user_id, created_at, updated_at)"
            + " values (?, ?, ?, ?, ?, ?)",
        UUID.randomUUID(),
        kind,
        status,
        userId,
        Timestamp.from(at),
        Timestamp.from(at));
  }

  // 같은 단어는 한 번만 저장할 수 있으므로(user_id, vocab_id unique) 저장할 때마다 다른 시드 단어를 쓴다.
  private void wordSave(Long userId, String sourceType, Instant at) {
    jdbcTemplate.update(
        "insert into user_vocab (user_id, vocab_id, source_type, saved_at) values (?,"
            + " (select id from vocabularies order by id offset ? limit 1), ?, ?)",
        userId,
        nextVocab.getAndIncrement(),
        sourceType,
        Timestamp.from(at));
  }

  // 같은 미션은 하루에 한 번만 기록되므로(user_id, mission_id, mission_date unique) 미션 날짜를 하루씩 바꾼다.
  private void missionDone(Long userId, Instant completedAt) {
    jdbcTemplate.update(
        "insert into user_missions (user_id, mission_id, mission_date, status, completed_at)"
            + " values (?, (select min(id) from missions), ?, 'DONE', ?)",
        userId,
        LocalDate.of(2030, 1, 1).plusDays(nextMissionDay.getAndIncrement()),
        Timestamp.from(completedAt));
  }

  // 그날(한국 날짜) lastSeenAt까지 activeSeconds 동안 머문 일별 접속 기록
  private void visit(Long userId, Instant lastSeenAt, long activeSeconds) {
    jdbcTemplate.update(
        "insert into user_daily_activities (user_id, activity_date, first_seen_at, last_seen_at,"
            + " active_seconds) values (?, ?, ?, ?, ?)",
        userId,
        LocalDate.ofInstant(lastSeenAt, ClockConfig.SERVICE_ZONE),
        Timestamp.from(lastSeenAt.minusSeconds(activeSeconds)),
        Timestamp.from(lastSeenAt),
        activeSeconds);
  }

  // 한국 날짜 오전 10시에 계정을 만든 가입자
  private Long startedOn(String koreanDay, String providerUid) {
    Long userId = user(AuthProvider.GOOGLE, providerUid, providerUid);
    createdAt(userId, koreanTenAm(koreanDay));
    return userId;
  }

  private static Instant koreanTenAm(String koreanDay) {
    return LocalDate.parse(koreanDay).atTime(10, 0).atZone(ClockConfig.SERVICE_ZONE).toInstant();
  }

  private void gameClock(Long userId, long playSeconds, Instant lastBeatAt) {
    jdbcTemplate.update(
        "insert into user_game_clocks (user_id, play_seconds, last_beat_at) values (?, ?, ?)",
        userId,
        playSeconds,
        Timestamp.from(lastBeatAt));
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    Clock clock() {
      return Clock.fixed(NOW, ZoneOffset.UTC);
    }
  }
}
