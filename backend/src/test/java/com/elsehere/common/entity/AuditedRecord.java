package com.elsehere.common.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/** BaseEntity의 시각 기록을 검증하기 위한 테스트 전용 엔티티. */
@Getter
@Entity
@Table(name = "audited_records")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
class AuditedRecord extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private String name;

  AuditedRecord(String name) {
    this.name = name;
  }

  void rename(String name) {
    this.name = name;
  }
}
