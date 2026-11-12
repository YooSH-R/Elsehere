package com.elsehere.admin.service;

import static java.time.temporal.ChronoUnit.DAYS;
import static java.time.temporal.ChronoUnit.HOURS;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.admin.dto.AiUsageReport;
import com.elsehere.admin.dto.AiUsageRow;
import com.elsehere.admin.dto.AiUserUsage;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.sql.Timestamp;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.UUID;
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

// 호출 기록은 실제 PostgreSQL에 SQL로 넣는다. 다른 테스트가 별도 트랜잭션으로 커밋한 기록과 섞이지 않도록 먼 미래 시각을 쓴다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  AdminAiUsageService.class,
  AdminAiUsageServiceTest.ClockTestConfiguration.class
})
class AdminAiUsageServiceTest {

  // 한국 시각 2030-01-10 09:00
  private static final Instant NOW = Instant.parse("2030-01-10T00:00:00Z");

  @Autowired private AdminAiUsageService adminAiUsageService;

  @Autowired private EntityManager entityManager;

  @Autowired private JdbcTemplate jdbcTemplate;

  @Test
  @DisplayName("기간 안의 호출을 종류별로 세고 실패 수·토큰·음성 길이를 더한다. 기간 밖과 예약만 된 기록은 뺀다")
  void report_countsByKindWithinPeriod() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    call(member, "NPC", "OK", "gpt-4o-mini", 100, 20, null, NOW.minus(1, HOURS));
    call(member, "NPC", "FAILED", null, 0, 0, null, NOW.minus(2, HOURS));
    call(member, "STT", "OK", "whisper-large-v3", 0, 0, 1500, NOW.minus(1, DAYS));
    call(member, "NPC", "OK", "gpt-4o-mini", 999, 999, null, NOW.minus(8, DAYS));
    call(member, "JUDGE", "RESERVED", null, 0, 0, null, NOW.minus(1, HOURS));

    AiUsageReport report = adminAiUsageService.report(7);

    assertThat(report.byKind())
        .containsExactly(
            new AiUsageRow("NPC", 1, 2, 1, 100, 20, 0), new AiUsageRow("STT", 1, 1, 0, 0, 0, 1500));
    assertThat(report.total()).isEqualTo(new AiUsageRow("전체", 1, 3, 1, 100, 20, 1500));
  }

  @Test
  @DisplayName("모델별로 센다. 모델을 알리지 않은 호출(실패·음성 합성)은 '없음'으로 묶는다")
  void report_countsByModel() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    call(member, "NPC", "OK", "gpt-4o-mini", 100, 20, null, NOW.minus(1, HOURS));
    call(member, "JUDGE", "OK", "gpt-4o-mini", 50, 5, null, NOW.minus(1, HOURS));
    call(member, "TTS", "OK", null, 0, 0, null, NOW.minus(1, HOURS));

    AiUsageReport report = adminAiUsageService.report(7);

    assertThat(report.byModel())
        .containsExactly(
            new AiUsageRow("gpt-4o-mini", 1, 2, 0, 150, 25, 0),
            new AiUsageRow("없음", 1, 1, 0, 0, 0, 0));
  }

  @Test
  @DisplayName("날짜는 한국 날짜로 묶어 오래된 날부터 보여 준다")
  void report_countsByKoreanDay() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    // 한국 시각 2030-01-09 23:59와 2030-01-10 00:00
    call(member, "NPC", "OK", "gpt-4o-mini", 10, 1, null, Instant.parse("2030-01-09T14:59:00Z"));
    call(member, "NPC", "OK", "gpt-4o-mini", 20, 2, null, Instant.parse("2030-01-09T15:00:00Z"));

    AiUsageReport report = adminAiUsageService.report(7);

    assertThat(report.byDay())
        .containsExactly(
            new AiUsageRow("2030-01-09", 1, 1, 0, 10, 1, 0),
            new AiUsageRow("2030-01-10", 1, 1, 0, 20, 2, 0));
  }

  @Test
  @DisplayName("게스트와 가입자로 나눠 세고, 토큰을 많이 쓴 사용자부터 가입자는 이메일로, 게스트는 '게스트 #ID'로 보여 준다")
  void report_countsByUserTypeAndTopUsers() {
    Long member = user(AuthProvider.GOOGLE, "google-sub-1", "Sun");
    jdbcTemplate.update("update users set email = 'sun@example.com' where id = ?", member);
    Long guest = user(AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    call(member, "NPC", "OK", "gpt-4o-mini", 100, 20, null, NOW.minus(1, HOURS));
    call(guest, "NPC", "OK", "gpt-4o-mini", 300, 30, null, NOW.minus(1, HOURS));
    call(guest, "NPC", "FAILED", null, 0, 0, null, NOW.minus(1, HOURS));

    AiUsageReport report = adminAiUsageService.report(7);

    assertThat(report.byUserType())
        .containsExactly(
            new AiUsageRow("GUEST", 1, 2, 1, 300, 30, 0),
            new AiUsageRow("MEMBER", 1, 1, 0, 100, 20, 0));
    assertThat(report.topUsers())
        .containsExactly(
            new AiUserUsage(guest, true, "게스트 #" + guest, 2, 1, 300, 30),
            new AiUserUsage(member, false, "sun@example.com", 1, 0, 100, 20));
  }

  @ParameterizedTest
  @ValueSource(ints = {0, -1, 91})
  @DisplayName("기간이 1~90일을 벗어나면 VALIDATION_FAILED로 거절한다")
  void report_daysOutOfRange_isRejected(int days) {
    assertThatThrownBy(() -> adminAiUsageService.report(days))
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

  private void call(
      Long userId,
      String kind,
      String status,
      String model,
      int inputTokens,
      int outputTokens,
      Integer audioMs,
      Instant at) {
    jdbcTemplate.update(
        "insert into ai_requests (request_id, kind, status, user_id, model, input_tokens,"
            + " output_tokens, audio_ms, created_at, updated_at)"
            + " values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        UUID.randomUUID(),
        kind,
        status,
        userId,
        model,
        inputTokens,
        outputTokens,
        audioMs,
        Timestamp.from(at),
        Timestamp.from(at));
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    Clock clock() {
      return Clock.fixed(NOW, ZoneOffset.UTC);
    }
  }
}
