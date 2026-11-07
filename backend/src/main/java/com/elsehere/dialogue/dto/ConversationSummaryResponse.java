package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.feedback.entity.FeedbackStatus;
import java.time.OffsetDateTime;

/**
 * 지난 대화 목록(CONV-09)의 대화 하나.
 *
 * @param userTurnCount 사용자가 보낸 발화 수
 * @param feedbackStatus 짧은 피드백 상태. 피드백을 만들기 전(진행 중인 대화 등)이면 null
 */
public record ConversationSummaryResponse(
    Long conversationId,
    NpcSummaryResponse npc,
    String sceneCode,
    ConversationStatus status,
    ConversationEndReason endReason,
    OffsetDateTime startedAt,
    OffsetDateTime endedAt,
    int userTurnCount,
    FeedbackStatus feedbackStatus,
    boolean feedbackUnread) {

  public ConversationSummaryResponse(
      Long conversationId,
      NpcSummaryResponse npc,
      String sceneCode,
      ConversationStatus status,
      ConversationEndReason endReason,
      OffsetDateTime startedAt,
      OffsetDateTime endedAt,
      int userTurnCount,
      FeedbackStatus feedbackStatus) {
    this(
        conversationId,
        npc,
        sceneCode,
        status,
        endReason,
        startedAt,
        endedAt,
        userTurnCount,
        feedbackStatus,
        false);
  }
}
