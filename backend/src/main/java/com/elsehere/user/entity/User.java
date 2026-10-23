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

  /** 학습 언어를 고르기 전(ONB-02 전)에 저장하는 기본 언어. current_language가 필수 컬럼이라 둔다. */
  public static final String DEFAULT_LANGUAGE = "en-US";

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

  /** 가입한 사용자. 초기 설정은 학습 언어 선택(LANG)부터 시작한다. */
  public static User signUp(
      AuthProvider provider,
      String providerUid,
      String email,
      String nickname,
      AvatarType avatarType) {
    User user = new User();
    user.provider = provider;
    user.providerUid = providerUid;
    user.email = email;
    user.nickname = nickname;
    user.avatarType = avatarType;
    user.onboardingStep = OnboardingStep.LANG;
    user.currentLanguage = DEFAULT_LANGUAGE;
    return user;
  }

  /** 학습 언어를 바꾼다. 처음 고를 때만 초기 설정을 레벨 테스트 단계로 넘긴다(ONB-02). */
  public void chooseLanguage(String languageCode) {
    this.currentLanguage = languageCode;
    if (onboardingStep == OnboardingStep.LANG) {
      this.onboardingStep = OnboardingStep.LEVEL;
    }
  }

  /** 대화 난이도 설정(ONB-09)을 마쳐 튜토리얼 이후 단계에 있는지. */
  public boolean hasCompletedLevelSetting() {
    return onboardingStep == OnboardingStep.TUTORIAL || onboardingStep == OnboardingStep.DONE;
  }

  /** 대화 난이도 설정을 마친다. 레벨 테스트 단계에서만 튜토리얼 단계로 넘긴다(ONB-09). */
  public void completeLevelSetting() {
    if (onboardingStep == OnboardingStep.LEVEL) {
      this.onboardingStep = OnboardingStep.TUTORIAL;
    }
  }
}
