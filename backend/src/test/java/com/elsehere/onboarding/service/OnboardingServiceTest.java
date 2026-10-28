package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.LanguageProfileResponse;
import com.elsehere.onboarding.dto.LanguageSelectionResponse;
import com.elsehere.onboarding.dto.LevelSelectionResponse;
import com.elsehere.onboarding.dto.LevelTestSummary;
import com.elsehere.onboarding.entity.LevelTest;
import com.elsehere.onboarding.entity.LevelTestStatus;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.User;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  OnboardingService.class,
  LanguageCatalog.class,
  LevelCatalog.class
})
class OnboardingServiceTest {

  @Autowired private EntityManager entityManager;

  @Autowired private OnboardingService onboardingService;

  @Autowired private UserLanguageProfileRepository profileRepository;

  @Test
  @DisplayName("학습 언어를 고르면 언어별 학습 설정을 만들고 사용자를 레벨 테스트 단계로 넘긴다")
  void chooseLanguage_createsProfileAndMovesToLevelStep() {
    User user = persistNewUser();

    LanguageSelectionResponse response = onboardingService.chooseLanguage(user.getId(), "en-US");

    assertThat(response.languageCode()).isEqualTo("en-US");
    assertThat(response.onboardingStep()).isEqualTo(OnboardingStep.LEVEL);
    UserLanguageProfile profile =
        profileRepository.findById(new UserLanguageProfileId(user.getId(), "en-US")).orElseThrow();
    assertThat(profile.getSelectedLevel()).isEqualTo(1);
    assertThat(profile.getRecommendedLevel()).isNull();
    assertThat(user.getCurrentLanguage()).isEqualTo("en-US");
  }

  @Test
  @DisplayName("고를 수 없는 언어면 VALIDATION_FAILED로 거절하고 학습 설정을 만들지 않는다")
  void chooseUnavailableLanguage_isRejected() {
    User user = persistNewUser();

    assertThatThrownBy(() -> onboardingService.chooseLanguage(user.getId(), "ja-JP"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
    assertThat(profileRepository.count()).isZero();
    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.LANG);
  }

  @Test
  @DisplayName("같은 언어를 다시 고르면 기존 학습 설정을 새로 만들거나 덮어쓰지 않는다")
  void chooseSameLanguageAgain_keepsExistingProfile() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");
    entityManager
        .createNativeQuery("update user_language_profiles set selected_level = 3 where user_id = ?")
        .setParameter(1, user.getId())
        .executeUpdate();
    entityManager.clear();

    onboardingService.chooseLanguage(user.getId(), "en-US");

    assertThat(profileRepository.count()).isEqualTo(1);
    assertThat(
            profileRepository
                .findById(new UserLanguageProfileId(user.getId(), "en-US"))
                .orElseThrow()
                .getSelectedLevel())
        .isEqualTo(3);
  }

  @Test
  @DisplayName("난이도를 정하기 전에는 선택 난이도 없이 학습 설정과 고를 수 있는 난이도 단계를 돌려준다")
  void getLanguageProfileBeforeLevelSetting_hasNoSelectedLevel() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");

    LanguageProfileResponse response = onboardingService.getLanguageProfile(user.getId(), "en-US");

    assertThat(response.languageCode()).isEqualTo("en-US");
    assertThat(response.recommendedLevel()).isNull();
    assertThat(response.selectedLevel()).isNull();
    assertThat(response.levels())
        .extracting(ConversationLevel::level, ConversationLevel::available)
        .containsExactly(tuple(1, true), tuple(2, false), tuple(3, false));
  }

  @Test
  @DisplayName("난이도 단계는 초급·중급·고급 이름으로 보여준다")
  void getLanguageProfile_namesLevelsBeginnerIntermediateAdvanced() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");

    LanguageProfileResponse response = onboardingService.getLanguageProfile(user.getId(), "en-US");

    assertThat(response.levels())
        .extracting(ConversationLevel::name)
        .containsExactly("초급", "중급", "고급");
  }

  @Test
  @DisplayName("레벨 테스트 기록이 없으면 학습 설정 조회에 마지막 테스트를 담지 않는다")
  void getLanguageProfileWithoutLevelTest_hasNoLastLevelTest() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");

    LanguageProfileResponse response = onboardingService.getLanguageProfile(user.getId(), "en-US");

    assertThat(response.levelTest()).isNull();
  }

  @Test
  @DisplayName("마지막 레벨 테스트가 있으면 학습 설정 조회에 그 테스트의 ID와 상태를 담는다")
  void getLanguageProfileAfterLevelTest_hasLastLevelTest() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");
    LevelTest skipped =
        LevelTest.skip(user.getId(), "en-US", Instant.parse("2026-10-26T03:00:00Z"));
    entityManager.persist(skipped);
    profileRepository
        .findById(new UserLanguageProfileId(user.getId(), "en-US"))
        .orElseThrow()
        .recordLastTest(skipped.getId());

    LanguageProfileResponse response = onboardingService.getLanguageProfile(user.getId(), "en-US");

    assertThat(response.levelTest())
        .isEqualTo(new LevelTestSummary(skipped.getId(), LevelTestStatus.SKIPPED));
  }

  @Test
  @DisplayName("고른 적 없는 언어의 학습 설정을 조회하면 NOT_FOUND로 응답한다")
  void getLanguageProfileWithoutProfile_isNotFound() {
    User user = persistNewUser();

    assertThatThrownBy(() -> onboardingService.getLanguageProfile(user.getId(), "en-US"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("난이도를 정하면 저장하고 튜토리얼 단계로 넘어가며, 이후 조회에 선택 난이도가 보인다")
  void selectLevel_savesLevelAndMovesToTutorialStep() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");

    LevelSelectionResponse response = onboardingService.selectLevel(user.getId(), "en-US", 1);

    assertThat(response.selectedLevel()).isEqualTo(1);
    assertThat(response.onboardingStep()).isEqualTo(OnboardingStep.TUTORIAL);
    assertThat(onboardingService.getLanguageProfile(user.getId(), "en-US").selectedLevel())
        .isEqualTo(1);
  }

  @Test
  @DisplayName("아직 제공하지 않는 난이도를 고르면 VALIDATION_FAILED로 거절하고 단계를 바꾸지 않는다")
  void selectUnavailableLevel_isRejected() {
    User user = persistNewUser();
    onboardingService.chooseLanguage(user.getId(), "en-US");

    assertThatThrownBy(() -> onboardingService.selectLevel(user.getId(), "en-US", 2))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.LEVEL);
  }

  @Test
  @DisplayName("고른 적 없는 언어의 난이도를 정하면 NOT_FOUND로 응답한다")
  void selectLevelWithoutProfile_isNotFound() {
    User user = persistNewUser();

    assertThatThrownBy(() -> onboardingService.selectLevel(user.getId(), "en-US", 1))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  private User persistNewUser() {
    User user =
        User.signUp(AuthProvider.GOOGLE, "google-sub-1", "sun@example.com", "Sun", AvatarType.F);
    entityManager.persist(user);
    return user;
  }
}
