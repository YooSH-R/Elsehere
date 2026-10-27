package com.elsehere.feedback.dto;

import com.elsehere.feedback.entity.FeedbackStatus;

/**
 * 화면 16의 짧은 피드백.
 *
 * @param summary 대화 요약. DONE이 아니면 null
 * @param suggestion 개선 표현 상자. 고칠 표현이 없으면 null
 * @param errorCode FAILED·SKIPPED의 이유(AI_TIMEOUT, NO_USABLE_USER_TURNS 등). 사용자가 건너뛰었으면 null
 */
public record FeedbackResponse(
    FeedbackStatus status,
    String summary,
    ReviewsResponse reviews,
    SuggestionResponse suggestion,
    String errorCode) {}
