package com.elsehere.onboarding.service;

import java.util.Set;

/**
 * 레벨 테스트 말하기 답을 AI가 판정한 결과.
 *
 * @param passedItemCodes 통과한 말하기 문항
 * @param feedback 결과 화면의 한국어 한 줄 피드백. 판정할 답이 없었으면 null
 */
public record SpeakingJudgement(Set<String> passedItemCodes, String feedback) {

  /** 답한 말하기 문항이 없어 AI에 묻지 않은 경우. */
  public static SpeakingJudgement none() {
    return new SpeakingJudgement(Set.of(), null);
  }

  public boolean passed(String itemCode) {
    return passedItemCodes.contains(itemCode);
  }
}
