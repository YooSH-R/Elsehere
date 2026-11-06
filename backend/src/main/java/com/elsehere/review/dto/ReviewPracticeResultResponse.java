package com.elsehere.review.dto;

import java.time.Instant;
import java.util.List;

public record ReviewPracticeResultResponse(
    Long conversationId,
    Long reviewCardId,
    boolean practiceCompleted,
    int verifiedExpressionCount,
    int practiceCount,
    Instant completedAt,
    List<ReviewExpressionUsage> expressions,
    List<ReviewUtterance> utterances) {}
