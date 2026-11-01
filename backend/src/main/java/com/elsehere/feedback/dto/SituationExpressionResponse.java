package com.elsehere.feedback.dto;

/**
 * 이 장면에서 자주 쓰는 관용·추천 표현. 사용자 발화 평가와 별개로 다음에 같은 상황에서 써 볼 표현이다.
 *
 * @param usageKo 이 장면에서 언제 쓰는지
 * @param exampleEn 학습용 새 예문. 사용자가 한 말이 아니다
 */
public record SituationExpressionResponse(
    String expression, String meaningKo, String usageKo, String exampleEn, String exampleKo) {}
