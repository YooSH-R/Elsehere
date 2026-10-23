package com.elsehere.user.entity;

import com.elsehere.common.entity.BaseEntity;
import com.elsehere.world.entity.Scene;
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
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/** 사용자. 소셜 로그인 계정({@code provider}, {@code providerUid}) 하나에 사용자 하나다. */
@Entity
@Table(
    name = "users",
    uniqueConstraints = @UniqueConstraint(columnNames = {"provider", "provider_uid"}))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class User extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private AuthProvider provider;

  // 제공자가 주는 사용자 고유 ID. Google은 OIDC의 sub 값이다.
  @Column(nullable = false, length = 100)
  private String providerUid;

  @Column(length = 255)
  private String email;

  @Column(nullable = false, length = 30)
  private String nickname;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 10)
  private AvatarType avatarType;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private OnboardingStep onboardingStep;

  @Column(nullable = false, length = 10)
  private String currentLanguage;

  // 재접속 위치
  @ManyToOne(fetch = FetchType.LAZY)
  @JoinColumn(name = "last_scene_id")
  private Scene lastScene;
}
