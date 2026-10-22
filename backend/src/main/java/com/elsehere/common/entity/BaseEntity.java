package com.elsehere.common.entity;

import jakarta.persistence.Column;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.MappedSuperclass;
import java.time.Instant;
import lombok.Getter;
import org.hibernate.annotations.ColumnDefault;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.LastModifiedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

/**
 * 모든 엔티티가 상속하는 생성·수정 시각.
 *
 * <p>시각은 {@link com.elsehere.common.config.JpaAuditingConfig}가 주입한 {@code Clock}으로 채운다. 식별자는 복합키
 * 테이블이 있어 각 엔티티에서 선언한다.
 *
 * <p>시드 SQL처럼 JPA를 거치지 않고 넣는 행을 위해 DB 기본값 {@code now()}도 둔다.
 */
@Getter
@MappedSuperclass
@EntityListeners(AuditingEntityListener.class)
public abstract class BaseEntity {

  @CreatedDate
  @ColumnDefault("now()")
  @Column(name = "created_at", nullable = false, updatable = false)
  private Instant createdAt;

  @LastModifiedDate
  @ColumnDefault("now()")
  @Column(name = "updated_at", nullable = false)
  private Instant updatedAt;
}
