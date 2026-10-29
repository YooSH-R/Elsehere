package com.elsehere.feedback.dto;

import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/** 피드백 다시 만들기(FB-02), 상세 평가(FB-03), 모범 음성(FB-04) 요청. */
public record FeedbackRequest(@NotNull UUID requestId) {}
