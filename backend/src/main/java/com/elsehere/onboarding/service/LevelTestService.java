package com.elsehere.onboarding.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.LevelTestItemResponse;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import com.elsehere.onboarding.dto.LevelTestStartResponse;
import com.elsehere.onboarding.entity.LevelTest;
import com.elsehere.onboarding.entity.LevelTestAnswer;
import com.elsehere.onboarding.entity.LevelTestStatus;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.LevelTestAnswerRepository;
import com.elsehere.onboarding.repository.LevelTestRepository;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 레벨 테스트(ONB-03~07). 듣기 4문항과 말하기 2문항으로 초급(1)과 중급(2) 중 추천 난이도를 정한다.
 *
 * <p>학습 설정의 마지막 테스트(last_test_id)는 시작·건너뛰기 때 바꾼다. ONB-08이 그 상태로 화면 04·05를 나눈다. 말하기 답의 AI 판정은 DB
 * 트랜잭션 밖에서 {@link LevelTestCompletionService}가 묻고, 그 결과로 여기서 완료한다.
 */
@Service
@RequiredArgsConstructor
@Transactional
public class LevelTestService {

  private static final int BEGINNER = 1;
  private static final int MIDDLE = 2;

  // 중급은 듣기 4개 중 3개 이상을 맞히고 말하기를 모두 통과해야 한다.
  private static final int LISTENING_FOR_MIDDLE = 3;

  private final LevelTestRepository levelTestRepository;
  private final LevelTestAnswerRepository answerRepository;
  private final UserLanguageProfileRepository profileRepository;
  private final LevelTestCatalog levelTestCatalog;
  private final Clock clock;

  public LevelTestStartResponse start(Long userId, String languageCode) {
    UserLanguageProfile profile = findProfile(userId, languageCode);
    LevelTest test =
        levelTestRepository.save(LevelTest.start(userId, languageCode, Instant.now(clock)));
    profile.recordLastTest(test.getId());

    List<LevelTestItemResponse> items =
        levelTestCatalog.items().stream().map(LevelTestService::itemResponse).toList();
    return new LevelTestStartResponse(test.getId(), test.getStatus(), items.size(), items);
  }

  // 문항 응답에는 정답·대사·판정 기준을 담지 않는다.
  private static LevelTestItemResponse itemResponse(LevelTestItem item) {
    return new LevelTestItemResponse(
        item.itemCode(),
        LevelTestItemResponse.Type.valueOf(item.type().name()),
        item.instruction(),
        item.question(),
        item.choices(),
        item.speaker(),
        item.cues());
  }

  /** 듣기 문항은 고른 보기를 정답과 비교해 기록한다. 말하기 문항은 받아 적은 말만 저장하고, 통과 여부는 완료할 때 정한다. */
  public void answer(Long userId, Long testId, String itemCode, String answer) {
    LevelTest test = findTestInProgress(userId, testId);
    LevelTestItem item =
        levelTestCatalog
            .findItem(itemCode)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    boolean correct = item.isListening() && item.answer().equals(answer);
    answerRepository
        .findByTestAndItemCode(test, itemCode)
        .ifPresentOrElse(
            previous -> previous.change(answer, correct),
            () -> answerRepository.save(LevelTestAnswer.record(test, itemCode, answer, correct)));
  }

  /** AI 판정에 보낼, 답한 말하기 문항과 그 답. 진행 중인 테스트에만 물을 수 있다. */
  @Transactional(readOnly = true)
  public List<LevelTestSpeakingAnswer> speakingAnswers(Long userId, Long testId) {
    LevelTest test = findTestInProgress(userId, testId);
    return answerRepository.findAllByTest(test).stream()
        .flatMap(
            answer ->
                levelTestCatalog
                    .findItem(answer.getItemCode())
                    .filter(LevelTestItem::isSpeaking)
                    .map(item -> new LevelTestSpeakingAnswer(item, answer.getAnswer()))
                    .stream())
        .toList();
  }

  /**
   * 듣기는 맞힌 개수를, 말하기는 AI 판정으로 통과한 개수를 센다. 듣기 3개 이상과 말하기 전부를 통과하면 중급, 그 밖은 초급을 추천한다. 답하지 않은 문항은 틀린
   * 것으로 센다.
   */
  public LevelTestResultResponse complete(Long userId, Long testId, SpeakingJudgement judgement) {
    LevelTest test = findTestInProgress(userId, testId);
    int listeningScore = 0;
    int speakingPassed = 0;
    for (LevelTestAnswer answer : answerRepository.findAllByTest(test)) {
      LevelTestItem item = levelTestCatalog.findItem(answer.getItemCode()).orElse(null);
      if (item == null) {
        continue;
      }
      if (item.isSpeaking()) {
        boolean passed = judgement.passed(item.itemCode());
        answer.change(answer.getAnswer(), passed);
        speakingPassed += passed ? 1 : 0;
      } else if (answer.isCorrect()) {
        listeningScore++;
      }
    }
    int resultLevel =
        listeningScore >= LISTENING_FOR_MIDDLE && speakingPassed == speakingItemCount()
            ? MIDDLE
            : BEGINNER;
    test.complete(listeningScore + speakingPassed, resultLevel, Instant.now(clock));
    findProfile(userId, test.getLanguageCode()).recommendLevel(resultLevel);
    return new LevelTestResultResponse(
        test.getId(),
        test.getStatus(),
        test.getScore(),
        test.getResultLevel(),
        listeningScore,
        speakingPassed,
        judgement.feedback());
  }

  /** 일부만 풀고 멈춘 기록이다. 추천 난이도는 바꾸지 않는다. */
  public LevelTestResultResponse quit(Long userId, Long testId) {
    LevelTest test = findTestInProgress(userId, testId);
    test.quit(Instant.now(clock));
    return LevelTestResultResponse.ended(test.getId(), test.getStatus());
  }

  /** [건너뛰기]는 테스트를 시작하지 않고 누르므로 테스트 ID 없이 건너뛴 기록을 바로 만든다. */
  public LevelTestResultResponse skip(Long userId, String languageCode) {
    UserLanguageProfile profile = findProfile(userId, languageCode);
    LevelTest test =
        levelTestRepository.save(LevelTest.skip(userId, languageCode, Instant.now(clock)));
    profile.recordLastTest(test.getId());
    return LevelTestResultResponse.ended(test.getId(), test.getStatus());
  }

  private long speakingItemCount() {
    return levelTestCatalog.items().stream().filter(LevelTestItem::isSpeaking).count();
  }

  /** 응답·완료·그만하기는 진행 중인 테스트에만 할 수 있다. */
  private LevelTest findTestInProgress(Long userId, Long testId) {
    LevelTest test = findOwnTest(userId, testId);
    if (test.getStatus() != LevelTestStatus.IN_PROGRESS) {
      throw new BusinessException(ErrorCode.LEVEL_TEST_NOT_IN_PROGRESS);
    }
    return test;
  }

  /** 다른 사용자의 테스트는 있는지 드러내지 않도록 없는 테스트와 같게 응답한다. */
  private LevelTest findOwnTest(Long userId, Long testId) {
    return levelTestRepository
        .findByIdAndUserId(testId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 고른 적 없는 언어면 학습 설정이 없다. */
  private UserLanguageProfile findProfile(Long userId, String languageCode) {
    return profileRepository
        .findById(new UserLanguageProfileId(userId, languageCode))
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }
}
