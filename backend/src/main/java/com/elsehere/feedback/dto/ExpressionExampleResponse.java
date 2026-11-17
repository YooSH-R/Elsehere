package com.elsehere.feedback.dto;

/**
 * 사용자 발화와 구별하는 학습 예문. 모호한 요청은 conditionKo에 의도별 조건을 표시한다. 실시간 추천이 영어만 저장한 경우 meaningKo는 null이며 조회 중
 * 번역을 새로 생성하지 않는다.
 */
public record ExpressionExampleResponse(String conditionKo, String expression, String meaningKo) {}
