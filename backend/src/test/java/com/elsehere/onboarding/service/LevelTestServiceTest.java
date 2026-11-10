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
import java.util.List;
import java.util.Set;
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

  @Autowired private LevelTestCatalog levelTestCatalog;

  @Test
  @DisplayName("시작하면 진행 중인 테스트와 듣기 4·말하기 2 문항을 돌려주고 학습 설정의 마지막 테스트를 이 테스트로 바꾼다")
  void start_createsInProgressTestAndRecordsItAsLastTest() {
    User user = persistUserWithLanguage();

    LevelTestStartResponse response = levelTestService.start(user.getId(), "en-US");

    assertThat(response.status()).isEqualTo(LevelTestStatus.IN_PROGRESS);
    assertThat(response.totalItems()).isEqualTo(6);
    assertThat(response.items())
        .extracting(LevelTestItemResponse::itemCode, LevelTestItemResponse::type)
        .containsExactly(
            tuple("EN_LT_L1", LevelTestItemType.LISTEN),
            tuple("EN_LT_L2", LevelTestItemType.LISTEN),
            tuple("EN_LT_L3", LevelTestItemType.LISTEN),
            tuple("EN_LT_L4", LevelTestItemType.LISTEN),
            tuple("EN_LT_S1", LevelTestItemType.SPEAK),
            tuple("EN_LT_S2", LevelTestItemType.SPEAK));
    assertThat(findProfile(user).getLastTestId()).isEqualTo(response.testId());
  }

  @Test
  @DisplayName("듣기 문항은 말한 사람과 보기 4개를, 말하기 문항은 상황과 말할 정보를 담는다")
  void start_itemsCarryWhatEachScreenShows() {
    User user = persistUserWithLanguage();

    List<LevelTestItemResponse> items = levelTestService.start(user.getId(), "en-US").items();

    assertThat(items.get(1).speaker()).isEqualTo("Gus · 빨래방 주인");
    assertThat(items.get(1).choices()).hasSize(4);
    assertThat(items.get(4).question()).startsWith("카페에 가방을 두고 왔어요.");
    assertThat(items.get(4).cues()).containsExactly("파란 숄더백", "빨간 열쇠고리", "안에 여권 지갑과 책");
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
  @DisplayName("듣기 문항은 답하면 정답 여부를 기록하고, 말하기 문항은 받아 적은 말만 저장해 완료할 때 판정한다")
  void answer_savesAnswerWithCorrectness() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();

    levelTestService.answer(user.getId(), testId, "EN_LT_L1", correctChoice("EN_LT_L1"));
    levelTestService.answer(user.getId(), testId, "EN_LT_L2", wrongChoice("EN_LT_L2"));
    levelTestService.answer(user.getId(), testId, "EN_LT_S1", "I left my blue bag here.");

    assertThat(answerRepository.findAll())
        .extracting(LevelTestAnswer::getItemCode, LevelTestAnswer::isCorrect)
        .containsExactlyInAnyOrder(
            tuple("EN_LT_L1", true), tuple("EN_LT_L2", false), tuple("EN_LT_S1", false));
  }

  @Test
  @DisplayName("같은 문항에 다시 답하면 새로 쌓지 않고 덮어쓴다")
  void answerSameItemAgain_overwritesAnswer() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.answer(user.getId(), testId, "EN_LT_L1", wrongChoice("EN_LT_L1"));

    levelTestService.answer(user.getId(), testId, "EN_LT_L1", correctChoice("EN_LT_L1"));

    assertThat(answerRepository.findAll())
        .extracting(LevelTestAnswer::getItemCode, LevelTestAnswer::isCorrect)
        .containsExactly(tuple("EN_LT_L1", true));
  }

  @Test
  @DisplayName("없는 문항에 답하면 NOT_FOUND로 응답한다")
  void answerUnknownItem_isNotFound() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();

    assertThatThrownBy(() -> levelTestService.answer(user.getId(), testId, "EN_LT_X9", "have"))
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

    assertThatThrownBy(() -> levelTestService.answer(other.getId(), testId, "EN_LT_L1", "anything"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("판정에 보낼 말하기 응답은 답한 말하기 문항과 그 말이다")
  void speakingAnswers_areTheAnsweredSpeakingItems() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.answer(user.getId(), testId, "EN_LT_L1", correctChoice("EN_LT_L1"));
    levelTestService.answer(user.getId(), testId, "EN_LT_S2", "The stain is still there.");

    List<LevelTestSpeakingAnswer> answers = levelTestService.speakingAnswers(user.getId(), testId);

    assertThat(answers)
        .extracting(answer -> answer.item().itemCode(), LevelTestSpeakingAnswer::answer)
        .containsExactly(tuple("EN_LT_S2", "The stain is still there."));
  }

  @Test
  @DisplayName("듣기 3개 이상을 맞히고 말하기 2개를 모두 통과하면 중급(2)을 추천하고 저장한다")
  void completeWithListeningAndBothSpeakingPassed_recommendsMiddle() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    answerListening(user, testId, 3);
    levelTestService.answer(user.getId(), testId, "EN_LT_S1", "I left my blue shoulder bag here.");
    levelTestService.answer(user.getId(), testId, "EN_LT_S2", "The coffee stain is still there.");

    LevelTestResultResponse result =
        levelTestService.complete(
            user.getId(),
            testId,
            new SpeakingJudgement(Set.of("EN_LT_S1", "EN_LT_S2"), "설명을 잘했어요."));

    assertThat(result.status()).isEqualTo(LevelTestStatus.DONE);
    assertThat(result.listeningScore()).isEqualTo(3);
    assertThat(result.speakingPassed()).isEqualTo(2);
    assertThat(result.score()).isEqualTo(5);
    assertThat(result.resultLevel()).isEqualTo(2);
    assertThat(result.feedback()).isEqualTo("설명을 잘했어요.");
    assertThat(levelTestRepository.findById(testId).orElseThrow().getCompletedAt()).isEqualTo(NOW);
    assertThat(findProfile(user).getRecommendedLevel()).isEqualTo(2);
    assertThat(answerRepository.findAll())
        .filteredOn(answer -> answer.getItemCode().startsWith("EN_LT_S"))
        .allMatch(LevelTestAnswer::isCorrect);
  }

  @Test
  @DisplayName("듣기를 다 맞혀도 말하기를 하나라도 통과하지 못하면 초급(1)을 추천한다")
  void completeWithOneSpeakingFailed_recommendsBeginner() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    answerListening(user, testId, 4);
    levelTestService.answer(user.getId(), testId, "EN_LT_S1", "I left my blue shoulder bag here.");
    levelTestService.answer(user.getId(), testId, "EN_LT_S2", "shirt coffee");

    LevelTestResultResponse result =
        levelTestService.complete(
            user.getId(), testId, new SpeakingJudgement(Set.of("EN_LT_S1"), "부탁하는 말을 연습해 보세요."));

    assertThat(result.listeningScore()).isEqualTo(4);
    assertThat(result.speakingPassed()).isEqualTo(1);
    assertThat(result.resultLevel()).isEqualTo(1);
    assertThat(findProfile(user).getRecommendedLevel()).isEqualTo(1);
  }

  @Test
  @DisplayName("말하기를 모두 통과해도 듣기를 2개 이하로 맞히면 초급(1)이다. 답하지 않은 문항은 틀린 것으로 센다")
  void completeWithTwoListeningCorrect_recommendsBeginner() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    answerListening(user, testId, 2);
    levelTestService.answer(user.getId(), testId, "EN_LT_S1", "I left my blue shoulder bag here.");
    levelTestService.answer(user.getId(), testId, "EN_LT_S2", "The coffee stain is still there.");

    LevelTestResultResponse result =
        levelTestService.complete(
            user.getId(),
            testId,
            new SpeakingJudgement(Set.of("EN_LT_S1", "EN_LT_S2"), "듣기를 더 연습해 보세요."));

    assertThat(result.listeningScore()).isEqualTo(2);
    assertThat(result.resultLevel()).isEqualTo(1);
  }

  @Test
  @DisplayName("이미 끝난 테스트에 답하면 LEVEL_TEST_NOT_IN_PROGRESS로 거절한다")
  void answerFinishedTest_isRejected() {
    User user = persistUserWithLanguage();
    Long testId = levelTestService.start(user.getId(), "en-US").testId();
    levelTestService.complete(user.getId(), testId, SpeakingJudgement.none());

    assertThatThrownBy(() -> levelTestService.answer(user.getId(), testId, "EN_LT_L1", "anything"))
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
    levelTestService.complete(user.getId(), testId, SpeakingJudgement.none());

    assertThatThrownBy(
            () -> levelTestService.complete(user.getId(), testId, SpeakingJudgement.none()))
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
    levelTestService.answer(user.getId(), testId, "EN_LT_L1", correctChoice("EN_LT_L1"));

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
    levelTestService.complete(user.getId(), testId, SpeakingJudgement.none());

    assertThatThrownBy(() -> levelTestService.quit(user.getId(), testId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.LEVEL_TEST_NOT_IN_PROGRESS));
  }

  private void answerListening(User user, Long testId, int correctCount) {
    List<String> listening = List.of("EN_LT_L1", "EN_LT_L2", "EN_LT_L3", "EN_LT_L4");
    for (int i = 0; i < listening.size(); i++) {
      String itemCode = listening.get(i);
      String choice = i < correctCount ? correctChoice(itemCode) : wrongChoice(itemCode);
      levelTestService.answer(user.getId(), testId, itemCode, choice);
    }
  }

  private String correctChoice(String itemCode) {
    return levelTestCatalog.findItem(itemCode).orElseThrow().answer();
  }

  private String wrongChoice(String itemCode) {
    LevelTestItem item = levelTestCatalog.findItem(itemCode).orElseThrow();
    return item.choices().stream()
        .filter(choice -> !choice.equals(item.answer()))
        .findFirst()
        .orElseThrow();
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
