package com.elsehere.feedback.dto;

import java.util.List;

/** conversation_feedback.reviews에 저장하는 JSON. */
public record FeedbackReviews(
    List<String> good, List<String> improve, SuggestionResponse suggestion) {}
