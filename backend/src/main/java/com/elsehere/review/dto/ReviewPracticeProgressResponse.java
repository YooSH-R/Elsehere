package com.elsehere.review.dto;

import java.util.List;

/** 종료·횟수 기록 없이 현재 대화에서 확인한 목표 표현의 사용 근거. */
public record ReviewPracticeProgressResponse(
    Long conversationId,
    Long reviewCardId,
    int verifiedExpressionCount,
    List<ReviewExpressionUsage> expressions) {}
