package com.elsehere.feedback.dto;

import java.util.List;

/** FB-03 상세 평가. 사용자 발화 순서대로 담는다. */
public record DetailedFeedbackResponse(
    Long conversationId, List<DetailedFeedbackItemResponse> items) {}
