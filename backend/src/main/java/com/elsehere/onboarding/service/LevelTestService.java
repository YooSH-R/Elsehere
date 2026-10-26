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
 * 레벨 테스트(ONB-03~07). 방식이 정해지지 않아 MVP는 예시 문항과 고정 결과 레벨로 흐름만 제공한다.
 *
 * <p>학습 설정의 마지막 테스트(last_test_id)는 시작·건너뛰기 때 바꾼다. ONB-08이 그 상태로 화면 04·05를 나눈다.
 */
@Service
@RequiredArgsConstructor
@Transactional
public class LevelTestService {

  private static final int RESULT_LEVEL = 1;

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
        levelTestCatalog.items().stream().map(LevelTestItemResponse::from).toList();
    return new LevelTestStartResponse(test.getId(), test.getStatus(), items.size(), items);
  }

  public void answer(Long userId, Long testId, String itemCode, String answer) {
    LevelTest test = findTestInProgress(userId, testId);
    LevelTestItem item =
        levelTestCatalog
            .findItem(itemCode)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    boolean correct = item.answer().equals(answer);
    answerRepository
        .findByTestAndItemCode(test, itemCode)
        .ifPresentOrElse(
            previous -> previous.change(answer, correct),
            () -> answerRepository.save(LevelTestAnswer.record(test, itemCode, answer, correct)));
  }

  /** 채점은 맞힌 개수만 센다. 테스트 방식이 정해지기 전이라 결과 레벨은 점수와 상관없이 MVP에서 고를 수 있는 초급이다. */
  public LevelTestResultResponse complete(Long userId, Long testId) {
    LevelTest test = findTestInProgress(userId, testId);
    int score = answerRepository.countByTestAndCorrectTrue(test);
    test.complete(score, RESULT_LEVEL, Instant.now(clock));
    findProfile(userId, test.getLanguageCode()).recommendLevel(RESULT_LEVEL);
    return new LevelTestResultResponse(
        test.getId(), test.getStatus(), test.getScore(), test.getResultLevel());
  }

  /** 일부만 풀고 멈춘 기록이다. 추천 난이도는 바꾸지 않는다. */
  public LevelTestResultResponse quit(Long userId, Long testId) {
    LevelTest test = findTestInProgress(userId, testId);
    test.quit(Instant.now(clock));
    return new LevelTestResultResponse(test.getId(), test.getStatus(), null, null);
  }

  /** [건너뛰기]는 테스트를 시작하지 않고 누르므로 테스트 ID 없이 건너뛴 기록을 바로 만든다. */
  public LevelTestResultResponse skip(Long userId, String languageCode) {
    UserLanguageProfile profile = findProfile(userId, languageCode);
    LevelTest test =
        levelTestRepository.save(LevelTest.skip(userId, languageCode, Instant.now(clock)));
    profile.recordLastTest(test.getId());
    return new LevelTestResultResponse(test.getId(), test.getStatus(), null, null);
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
