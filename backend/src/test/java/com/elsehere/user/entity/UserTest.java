package com.elsehere.user.entity;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class UserTest {

  @Test
  @DisplayName("처음 학습 언어를 고르면 학습 언어가 바뀌고 레벨 테스트 단계로 넘어간다")
  void chooseLanguageFirstTime_movesToLevelStep() {
    User user = newUser();

    user.chooseLanguage("ja-JP");

    assertThat(user.getCurrentLanguage()).isEqualTo("ja-JP");
    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.LEVEL);
  }

  @Test
  @DisplayName("레벨 테스트 단계에서 난이도 설정을 마치면 튜토리얼 단계로 넘어간다")
  void completeLevelSettingAtLevelStep_movesToTutorialStep() {
    User user = newUser();
    user.chooseLanguage("en-US");

    user.completeLevelSetting();

    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.TUTORIAL);
  }

  @Test
  @DisplayName("학습 언어를 고르기 전에는 난이도 설정을 마쳐도 단계를 건너뛰지 않는다")
  void completeLevelSettingBeforeLanguage_keepsLanguageStep() {
    User user = newUser();

    user.completeLevelSetting();

    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.LANG);
  }

  @Test
  @DisplayName("언어 선택을 지난 사용자가 학습 언어를 다시 골라도 초기 설정 단계는 되돌아가지 않는다")
  void chooseLanguageAfterLanguageStep_keepsCurrentStep() {
    User user = newUser();
    user.chooseLanguage("en-US");
    user.completeLevelSetting();

    user.chooseLanguage("ja-JP");

    assertThat(user.getCurrentLanguage()).isEqualTo("ja-JP");
    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.TUTORIAL);
  }

  private static User newUser() {
    return User.signUp(AuthProvider.GOOGLE, "google-sub-1", "sun@example.com", "Sun", AvatarType.F);
  }
}
