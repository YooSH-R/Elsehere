package com.elsehere.review.dto;

/** 저장된 피드백에서 가져온 복습 표현. 복습을 열거나 다시 읽을 때 AI를 호출하지 않는다. */
public record ReviewTarget(
    String expression, String meaningKo, String usageKo, String exampleEn, String exampleKo) {}
