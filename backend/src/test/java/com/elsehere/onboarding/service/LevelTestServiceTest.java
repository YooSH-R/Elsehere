package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.LevelTestItemResponse;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import com.elsehere.onboarding.dto.LevelTestStartResponse;
import com.elsehere.onboarding.entity.LevelTestAnswer;
import com.elsehere.onboarding.entity.LevelTestStatus;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.LevelTestAnswerRepository;
import com.elsehere.onboarding.repository.LevelTestRepository;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.User;
import jakarta.persistence.EntityManager;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  LevelTestService.class,
  LevelTestCatalog.class,
  LevelTestServiceTest.FixedClockConfiguration.class
})
class LevelTestServiceTest {

  private static final Instant NOW = Instant.parse("2026-10-26T03:00:00Z");

  @Autowired private EntityManager entityManager;

  @Autowired private LevelTestService levelTestService;

  @Autowired private UserLanguageProfileRepository profileRepository;

  @Autowired private LevelTestAnswerRepository answerRepository;

  @Autowired private LevelTestRepository levelTestRepository;

  @Test
  @DisplayName("시작하면 진행 중인 테스트와 문항 5개를 돌려주고 학습 설정의 마지막 테스트를 이 테스트로 바꾼다")
  void start_createsInProgressTestAndRecordsItAsLastTest() {
    User user = persistUserWithLanguage();

    LevelTestStartResponse response = levelTestService.start(user.getId(), "en-US");

    assertThat(response.status()).isEqualTo(LevelTestStatus.IN_PROGRESS);
    assertThat(response.totalItems()).isEqualTo(5);
    assertThat(response.items())
        .extracting(LevelTestItemResponse::itemCode)
        .containsExactly("EN_L1_001", "EN_L1_002", "EN_L1_003", "EN_L1_004", "EN_L1_005");
    assertThat(findProfile(user).getLastTestId()).isEqualTo(response.testId());
  }

  @Test
  @DisplayName("고른 적 없는 언어로 시작하면 NOT_FOUND로 응답한다")
  void startWithoutChosenLanguage_isNotFound() {
    User user = persistUser();

    assertThatThrownBy(() -> levelTestService.start(user.getId(), "en-US"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("응답하면 문항별로 저장하고 정답 여부를 기록한다")
  void answer_savesAnswerWithCorrectness() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();

    levelTestService.answer(user.getId(), testId, "EN_L1_001", "have");
    levelTestService.answer(user.getId(), testId, "EN_L1_002", "are");

    assertThat(answerRepository.findAll())
        .extracting(
            LevelTestAnswer::getItemCode, LevelTestAnswer::getAnswer, LevelTestAnswer::isCorrect)
        .containsExactlyInAnyOrder(
            tuple("EN_L1_001", "have", true), tuple("EN_L1_002", "are", false));
  }

  @Test
  @DisplayName("같은 문항에 다시 답하면 새로 쌓지 않고 덮어쓴다")
  void answerSameItemAgain_overwritesAnswer() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.answer(user.getId(), testId, "EN_L1_001", "has");

    levelTestService.answer(user.getId(), testId, "EN_L1_001", "have");

    assertThat(answerRepository.findAll())
        .extracting(
            LevelTestAnswer::getItemCode, LevelTestAnswer::getAnswer, LevelTestAnswer::isCorrect)
        .containsExactly(tuple("EN_L1_001", "have", true));
  }

  @Test
  @DisplayName("없는 문항에 답하면 NOT_FOUND로 응답한다")
  void answerUnknownItem_isNotFound() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();

    assertThatThrownBy(() -> levelTestService.answer(user.getId(), testId, "EN_L9_999", "have"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("다른 사용자의 테스트에 답하면 있는지 드러내지 않고 NOT_FOUND로 응답한다")
  void answerOtherUsersTest_isNotFound() {
    User owner = persistUserWithLanguage();
    Long testId = levelTestService.start(owner.getId(), "en-US").testId();
    User other = persistUser("google-sub-2", "Moon");

    assertThatThrownBy(() -> levelTestService.answer(other.getId(), testId, "EN_L1_001", "have"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("완료하면 맞힌 개수를 점수로, 결과 레벨은 점수와 상관없이 초급(1)으로 정하고 추천 난이도를 1로 저장한다")
  void complete_scoresCorrectAnswersAndRecommendsBeginner() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.answer(user.getId(), testId, "EN_L1_001", "have");
    levelTestService.answer(user.getId(), testId, "EN_L1_002", "is");
    levelTestService.answer(user.getId(), testId, "EN_L1_003", "Yes, I do.");

    LevelTestResultResponse result = levelTestService.complete(user.getId(), testId);

    assertThat(result.status()).isEqualTo(LevelTestStatus.DONE);
    assertThat(result.score()).isEqualTo(2);
    assertThat(result.resultLevel()).isEqualTo(1);
    assertThat(levelTestRepository.findById(testId).orElseThrow().getCompletedAt()).isEqualTo(NOW);
    assertThat(findProfile(user).getRecommendedLevel()).isEqualTo(1);
  }

  @Test
  @DisplayName("이미 끝난 테스트에 답하면 LEVEL_TEST_NOT_IN_PROGRESS로 거절한다")
  void answerFinishedTest_isRejected() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.complete(user.getId(), testId);

    assertThatThrownBy(() -> levelTestService.answer(user.getId(), testId, "EN_L1_001", "have"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.LEVEL_TEST_NOT_IN_PROGRESS));
  }

  @Test
  @DisplayName("이미 끝난 테스트를 다시 완료하면 LEVEL_TEST_NOT_IN_PROGRESS로 거절한다")
  void completeFinishedTest_isRejected() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.complete(user.getId(), testId);

    assertThatThrownBy(() -> levelTestService.complete(user.getId(), testId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.LEVEL_TEST_NOT_IN_PROGRESS));
  }

  @Test
  @DisplayName("그만하면 점수와 추천 난이도 없이 그만둔 기록(QUIT)으로 끝낸다")
  void quit_endsTestWithoutRecommendation() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.answer(user.getId(), testId, "EN_L1_001", "have");

    LevelTestResultResponse result = levelTestService.quit(user.getId(), testId);

    assertThat(result.status()).isEqualTo(LevelTestStatus.QUIT);
    assertThat(result.score()).isNull();
    assertThat(levelTestRepository.findById(testId).orElseThrow().getCompletedAt()).isEqualTo(NOW);
    assertThat(findProfile(user).getRecommendedLevel()).isNull();
  }

  @Test
  @DisplayName("건너뛰면 문항 없이 건너뛴 기록(SKIPPED)을 만들고 마지막 테스트로 남기며 추천 난이도는 두지 않는다")
  void skip_recordsSkippedTestAsLastTest() {
    User user = persistUserWithLanguage();

    LevelTestResultResponse result = levelTestService.skip(user.getId(), "en-US");

    assertThat(result.status()).isEqualTo(LevelTestStatus.SKIPPED);
    UserLanguageProfile profile = findProfile(user);
    assertThat(profile.getLastTestId()).isEqualTo(result.testId());
    assertThat(profile.getRecommendedLevel()).isNull();
  }

  @Test
  @DisplayName("고른 적 없는 언어로 건너뛰면 NOT_FOUND로 응답한다")
  void skipWithoutChosenLanguage_isNotFound() {
    User user = persistUser();

    assertThatThrownBy(() -> levelTestService.skip(user.getId(), "en-US"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("이미 끝난 테스트를 그만두면 LEVEL_TEST_NOT_IN_PROGRESS로 거절한다")
  void quitFinishedTest_isRejected() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.complete(user.getId(), testId);

    assertThatThrownBy(() -> levelTestService.quit(user.getId(), testId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.LEVEL_TEST_NOT_IN_PROGRESS));
  }

  private User persistUser() {
    return persistUser("google-sub-1", "Sun");
  }

  private User persistUser(String providerUid, String nickname) {
    User user =
        User.signUp(AuthProvider.GOOGLE, providerUid, "sun@example.com", nickname, AvatarType.F);
    entityManager.persist(user);
    return user;
  }

  private User persistUserWithLanguage() {
    User user = persistUser();
    user.chooseLanguage("en-US");
    entityManager.persist(UserLanguageProfile.start(user, "en-US"));
    return user;
  }

  private UserLanguageProfile findProfile(User user) {
    return profileRepository
        .findById(new UserLanguageProfileId(user.getId(), "en-US"))
        .orElseThrow();
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class FixedClockConfiguration {

    @Bean
    Clock clock() {
      return Clock.fixed(NOW, ZoneOffset.UTC);
    }
  }
}
