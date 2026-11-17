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
import java.util.UUID;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/** 사용자. 소셜 로그인 계정({@code provider}, {@code providerUid}) 하나에 사용자 하나다. */
@Entity
@Table(
    name = "users",
    uniqueConstraints = {
      @UniqueConstraint(columnNames = {"provider", "provider_uid"}),
      @UniqueConstraint(name = User.NICKNAME_UNIQUE_CONSTRAINT, columnNames = "nickname")
    })
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class User extends BaseEntity {

  /** 학습 언어를 고르기 전(ONB-02 전)에 저장하는 기본 언어. current_language가 필수 컬럼이라 둔다. */
  public static final String DEFAULT_LANGUAGE = "en-US";

  /** 닉네임 중복을 막는 DB 제약 이름. 사전 검사를 지나친 동시 가입을 이 이름으로 알아본다. */
  public static final String NICKNAME_UNIQUE_CONSTRAINT = "uk_users_nickname";

  private static final String GUEST_NAME_FOR_NPCS = "kimssafy";

  // 회원 닉네임 규칙은 '-'를 허용하지 않으므로 탈퇴한 사용자의 닉네임은 회원·게스트 닉네임과 겹치지 않는다.
  private static final String WITHDRAWN_PREFIX = "withdrawn-";

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

  // 마이그레이션 전에 가입한 사용자와 SQL로 넣는 행도 기본 테마로 채우도록 DB 기본값을 둔다.
  @Enumerated(EnumType.STRING)
  @ColumnDefault("'JELLY'")
  @Column(nullable = false, length = 20)
  private UiTheme uiTheme;

  // 가입은 항상 일반 사용자다. 관리자는 다른 관리자가 어드민에서 지정한다(UserRole).
  @Enumerated(EnumType.STRING)
  @ColumnDefault("'USER'")
  @Column(nullable = false, length = 10)
  private UserRole role;

  // 재접속 위치
  @ManyToOne(fetch = FetchType.LAZY)
  @JoinColumn(name = "last_scene_id")
  private Scene lastScene;

  // 팀 기기(elsehere_team 쿠키를 남긴 브라우저)에서 쓴 계정. 어드민 이용 현황에서 뺀다.
  @ColumnDefault("false")
  @Column(nullable = false)
  private boolean teamAccount;

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
    user.uiTheme = UiTheme.JELLY;
    user.role = UserRole.USER;
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

  /** 튜토리얼을 마치거나 건너뛰어 초기 설정을 끝낸다(TUT-01·02). */
  public void finishTutorial() {
    this.onboardingStep = OnboardingStep.DONE;
  }

  /** 화면 테마를 바꾼다(USER-01). */
  public void changeUiTheme(UiTheme uiTheme) {
    this.uiTheme = uiTheme;
  }

  /** 닉네임을 바꾼다(USER-04). 규칙과 중복은 호출하는 쪽에서 확인한다. */
  public void changeNickname(String nickname) {
    this.nickname = nickname;
  }

  /** 게스트로 시작한 사용자(AUTH-07)인지. */
  public boolean isGuest() {
    return provider == AuthProvider.GUEST;
  }

  /**
   * NPC가 이 사용자를 부를 이름. 게스트 닉네임(guest-000123)은 사람 이름이 아니라서 게스트는 모두 같은 이름으로 부른다. 닉네임은 겹칠 수 없어 게스트 닉네임
   * 자체를 이 이름으로 바꾸지는 않는다.
   */
  public String nameForNpcs() {
    return isGuest() ? GUEST_NAME_FOR_NPCS : nickname;
  }

  /**
   * 게스트를 소셜 로그인 회원으로 바꾼다(AUTH-04). 같은 사용자 행이라 게스트 때 기록과 캐릭터·학습 언어·초기 설정 단계·마지막 장소가 그대로 이어진다. 닉네임
   * 규칙과 중복은 호출하는 쪽에서 확인한다.
   */
  public void becomeMember(
      AuthProvider provider, String providerUid, String email, String nickname) {
    if (!isGuest()) {
      throw new IllegalStateException("게스트만 회원으로 바꿀 수 있습니다.");
    }
    this.provider = provider;
    this.providerUid = providerUid;
    this.email = email;
    this.nickname = nickname;
  }

  /** 재접속할 장소를 바꾼다(WORLD-05). */
  public void moveTo(Scene scene) {
    this.lastScene = scene;
  }

  /** 관리자로 지정한다. 게스트가 아닌지는 호출하는 쪽에서 확인한다. */
  public void grantAdmin() {
    this.role = UserRole.ADMIN;
  }

  /** 팀 기기에서 쓴 계정으로 표시한다. 한 번 표시하면 되돌리지 않는다. */
  public void markTeamAccount() {
    this.teamAccount = true;
  }

  /** 관리자에서 해제해 일반 사용자로 되돌린다. */
  public void revokeAdmin() {
    this.role = UserRole.USER;
  }

  /**
   * 탈퇴한 사용자의 개인정보를 지운다(USER-05). 이메일을 비우고, 같은 계정과 닉네임으로 다시 가입할 수 있도록 제공자 ID와 닉네임을 탈퇴 표시 값으로 바꾸며,
   * 관리자 권한도 없앤다. 남아 있는 대화·사용량 기록이 이 행을 가리키므로 행은 지우지 않는다.
   */
  public void withdraw() {
    this.email = null;
    this.providerUid = WITHDRAWN_PREFIX + UUID.randomUUID();
    this.nickname = WITHDRAWN_PREFIX + id;
    this.role = UserRole.USER;
  }
}
