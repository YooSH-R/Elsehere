package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import java.time.OffsetDateTime;
import java.util.List;

/** 대화 정보와 모든 턴(CONV-10 상세, CONV-11 진행 중인 대화). */
public record ConversationDetailResponse(
    Long conversationId,
    ConversationStatus status,
    ConversationEndReason endReason,
    int levelApplied,
    NpcSummaryResponse npc,
    String sceneCode,
    OffsetDateTime startedAt,
    OffsetDateTime endedAt,
    int rudeCount,
    int rudeLimit,
    List<TurnDetailResponse> turns,
    Long reviewCardId) {
  public ConversationDetailResponse(
      Long conversationId,
      ConversationStatus status,
      ConversationEndReason endReason,
      int levelApplied,
      NpcSummaryResponse npc,
      String sceneCode,
      OffsetDateTime startedAt,
      OffsetDateTime endedAt,
      int rudeCount,
      int rudeLimit,
      List<TurnDetailResponse> turns) {
    this(
        conversationId,
        status,
        endReason,
        levelApplied,
        npc,
        sceneCode,
        startedAt,
        endedAt,
        rudeCount,
        rudeLimit,
        turns,
        null);
  }
}
