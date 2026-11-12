package com.elsehere.review.dto;

/**
 * 저장된 피드백에서 가져온 복습 목표. 복습을 열거나 다시 읽을 때 AI를 호출하지 않는다.
 *
 * @param originalText 고친 문장이면 학습자가 원래 한 말. 상황 표현과 이 칸을 넣기 전에 저장한 카드는 null
 */
public record ReviewTarget(
    String expression,
    String meaningKo,
    String usageKo,
    String exampleEn,
    String exampleKo,
    String originalText) {

  /** 이 상황에서 자주 쓰는 표현. */
  public ReviewTarget(
      String expression, String meaningKo, String usageKo, String exampleEn, String exampleKo) {
    this(expression, meaningKo, usageKo, exampleEn, exampleKo, null);
  }
}
