package com.elsehere.feedback.dto;

import java.util.List;

/**
 * conversation_feedback.reviews에 저장하는 JSON.
 *
 * @param situationExpressions 이 장면에서 자주 쓰는 표현. 이 필드를 넣기 전에 저장한 피드백은 null
 */
public record FeedbackReviews(
    List<String> good,
    List<String> improve,
    SuggestionResponse suggestion,
    List<SituationExpressionResponse> situationExpressions) {}
