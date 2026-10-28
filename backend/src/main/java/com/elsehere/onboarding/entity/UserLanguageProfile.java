package com.elsehere.onboarding.entity;

import com.elsehere.common.entity.BaseEntity;
import com.elsehere.user.entity.User;
import jakarta.persistence.Column;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.MapsId;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/** 사용자의 언어별 학습 설정. 레벨 테스트 추천 난이도와 사용자가 고른 대화 난이도를 둔다. */
@Entity
@Table(name = "user_language_profiles")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserLanguageProfile extends BaseEntity {

  /** 대화 난이도를 정하기 전(ONB-09 전)에 저장하는 값. selected_level이 필수 컬럼이라 둔다. */
  public static final int DEFAULT_SELECTED_LEVEL = 1;

  @EmbeddedId private UserLanguageProfileId id;

  @MapsId("userId")
  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "user_id")
  private User user;

  @Column(columnDefinition = "smallint")
  private Integer recommendedLevel;

  @Column(nullable = false, columnDefinition = "smallint")
  private Integer selectedLevel;

  // 마지막으로 시작하거나 건너뛴 레벨 테스트. ONB-08이 이 테스트의 상태로 화면 04·05를 나눈다.
  private Long lastTestId;

  public static UserLanguageProfile start(User user, String languageCode) {
    UserLanguageProfile profile = new UserLanguageProfile();
    profile.id = new UserLanguageProfileId(user.getId(), languageCode);
    profile.user = user;
    profile.selectedLevel = DEFAULT_SELECTED_LEVEL;
    return profile;
  }

  public void selectLevel(int level) {
    this.selectedLevel = level;
  }

  public void recordLastTest(Long testId) {
    this.lastTestId = testId;
  }

  public void recommendLevel(int level) {
    this.recommendedLevel = level;
  }
}
