package com.elsehere.feedback.dto;

/** 사용자가 한 말과 더 나은 표현. */
public record SuggestionResponse(String userText, String betterText) {}
