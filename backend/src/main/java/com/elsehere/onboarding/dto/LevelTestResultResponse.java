package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.entity.LevelTestStatus;
import com.fasterxml.jackson.annotation.JsonInclude;

/**
 * ONB-05~07 레벨 테스트를 끝낸 결과.
 *
 * <p>점수와 결과는 완료(DONE)했을 때만 담는다. 그만하기(QUIT)·건너뛰기(SKIPPED)는 상태만 담는다.
 *
 * @param score 맞힌 듣기 문항과 통과한 말하기 문항 수(0~6)
 * @param resultLevel 추천 난이도. 초급(1) 또는 중급(2)
 * @param listeningScore 맞힌 듣기 문항 수(0~4)
 * @param speakingPassed 통과한 말하기 문항 수(0~2)
 * @param feedback 결과 화면의 한국어 한 줄 피드백. 말하기 답이 없으면 담지 않는다
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record LevelTestResultResponse(
    Long testId,
    LevelTestStatus status,
    Integer score,
    Integer resultLevel,
    Integer listeningScore,
    Integer speakingPassed,
    String feedback) {

  /** 그만하기·건너뛰기처럼 점수 없이 끝난 기록. */
  public static LevelTestResultResponse ended(Long testId, LevelTestStatus status) {
    return new LevelTestResultResponse(testId, status, null, null, null, null, null);
  }
}
