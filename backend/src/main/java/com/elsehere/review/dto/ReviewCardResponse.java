package com.elsehere.review.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import java.time.Instant;
import java.util.List;

/** 사용자의 복습 상황 카드. 표현은 원본 피드백을 저장한 당시의 내용이다. */
public record ReviewCardResponse(
    Long id,
    Long sourceConversationId,
    String sceneCode,
    Long npcId,
    NpcSummaryResponse npc,
    String situationSummary,
    List<ReviewTarget> targets,
    int practiceCount,
    Instant lastPracticedAt,
    List<String> usedExpressions) {}
