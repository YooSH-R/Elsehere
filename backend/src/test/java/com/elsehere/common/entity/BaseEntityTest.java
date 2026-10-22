package com.elsehere.common.entity;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import jakarta.persistence.EntityManager;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

// 마이그레이션 도구를 아직 정하지 않아, 테스트용 엔티티의 테이블은 Hibernate가 만들고 지운다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  BaseEntityTest.ClockTestConfiguration.class
})
class BaseEntityTest {

  private static final Instant CREATED_TIME = Instant.parse("2026-10-22T01:00:00Z");
  private static final Instant UPDATED_TIME = Instant.parse("2026-10-22T02:30:00Z");

  @Autowired private EntityManager entityManager;

  @Autowired private AdjustableClock clock;

  @Test
  @DisplayName("처음 저장하면 생성 시각과 수정 시각이 현재 시각으로 기록된다")
  void persist_recordsCreatedAtAndUpdatedAt() {
    clock.setInstant(CREATED_TIME);
    AuditedRecord record = new AuditedRecord("first");

    entityManager.persist(record);
    entityManager.flush();

    assertThat(record.getCreatedAt()).isEqualTo(CREATED_TIME);
    assertThat(record.getUpdatedAt()).isEqualTo(CREATED_TIME);
  }

  @Test
  @DisplayName("수정하면 수정 시각만 바뀌고 생성 시각은 유지된다")
  void update_changesOnlyUpdatedAt() {
    clock.setInstant(CREATED_TIME);
    AuditedRecord record = new AuditedRecord("first");
    entityManager.persist(record);
    entityManager.flush();

    clock.setInstant(UPDATED_TIME);
    record.rename("second");
    entityManager.flush();
    entityManager.clear();

    AuditedRecord found = entityManager.find(AuditedRecord.class, record.getId());
    assertThat(found.getCreatedAt()).isEqualTo(CREATED_TIME);
    assertThat(found.getUpdatedAt()).isEqualTo(UPDATED_TIME);
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    AdjustableClock clock() {
      return new AdjustableClock(CREATED_TIME);
    }
  }

  /** 테스트 안에서 현재 시각을 바꿀 수 있는 Clock. */
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
