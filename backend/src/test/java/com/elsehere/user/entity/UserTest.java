package com.elsehere.user.entity;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;

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

  @ParameterizedTest
  @EnumSource(AuthProvider.class)
  @DisplayName("새 사용자는 로그인 방식(게스트 포함)과 관계없이 기본 화면 테마 JELLY로 시작한다")
  void signUp_startsWithJellyTheme(AuthProvider provider) {
    User user = User.signUp(provider, "provider-uid-1", null, "Sun", AvatarType.F);

    assertThat(user.getUiTheme()).isEqualTo(UiTheme.JELLY);
  }

  @Test
  @DisplayName("화면 테마를 바꾸면 고른 테마가 된다")
  void changeUiTheme_setsChosenTheme() {
    User user = newUser();

    user.changeUiTheme(UiTheme.STICKER);

    assertThat(user.getUiTheme()).isEqualTo(UiTheme.STICKER);
  }

  private static User newUser() {
    return User.signUp(AuthProvider.GOOGLE, "google-sub-1", "sun@example.com", "Sun", AvatarType.F);
  }
}
