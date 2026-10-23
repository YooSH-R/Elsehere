package com.elsehere.user.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/** 약관 동의 기록. 동의한 약관 버전과 시각을 남긴다. */
@Entity
@Table(name = "user_consents")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserConsent extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "user_id", nullable = false)
  private User user;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private ConsentType consentType;

  @Column(nullable = false, length = 20)
  private String version;

  @Column(nullable = false)
  private Instant agreedAt;

  public static UserConsent agree(
      User user, ConsentType consentType, String version, Instant agreedAt) {
    UserConsent consent = new UserConsent();
    consent.user = user;
    consent.consentType = consentType;
    consent.version = version;
    consent.agreedAt = agreedAt;
    return consent;
  }
}
