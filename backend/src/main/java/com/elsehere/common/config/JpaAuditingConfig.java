package com.elsehere.common.config;

import java.time.Clock;
import java.time.Instant;
import java.util.Optional;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.auditing.DateTimeProvider;
import org.springframework.data.jpa.repository.config.EnableJpaAuditing;

/**
 * 엔티티의 생성·수정 시각을 {@link Clock} 기준으로 기록한다.
 *
 * <p>메인 클래스가 아닌 별도 설정에 두어, JPA를 쓰지 않는 {@code @WebMvcTest} 같은 슬라이스 테스트에 영향을 주지 않는다.
 */
@Configuration(proxyBeanMethods = false)
@EnableJpaAuditing(dateTimeProviderRef = "auditingDateTimeProvider")
public class JpaAuditingConfig {

  @Bean
  public DateTimeProvider auditingDateTimeProvider(Clock clock) {
    return () -> Optional.of(Instant.now(clock));
  }
}
