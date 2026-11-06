package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.followup.dto.FollowUpResponse;
import java.time.OffsetDateTime;
import java.util.List;

/**
 * CONV-08 대화 종료 응답.
 *
 * @param feedback 짧은 피드백. 생성에 실패해도 대화는 종료되며 FAILED로 담긴다
 * @param completedMissions 이 대화에서 완료한 미션. 미션 연결 전까지 빈 목록이다
 * @param followUp NPC가 이 대화에서 곧 건네기로 한 것. 없으면 null
 */
public record EndConversationResponse(
    Long conversationId,
    ConversationStatus status,
    ConversationEndReason endReason,
    OffsetDateTime endedAt,
    NpcSummaryResponse npc,
    String sceneCode,
    long userTurnCount,
    FeedbackResponse feedback,
    List<Object> completedMissions,
    FollowUpResponse followUp,
    Long reviewCardId) {

  public EndConversationResponse(
      Long conversationId,
      ConversationStatus status,
      ConversationEndReason endReason,
      OffsetDateTime endedAt,
      NpcSummaryResponse npc,
      String sceneCode,
      long userTurnCount,
      FeedbackResponse feedback,
      List<Object> completedMissions,
      FollowUpResponse followUp) {
    this(
        conversationId,
        status,
        endReason,
        endedAt,
        npc,
        sceneCode,
        userTurnCount,
        feedback,
        completedMissions,
        followUp,
        null);
  }

  public EndConversationResponse withFeedback(FeedbackResponse feedback) {
    return new EndConversationResponse(
        conversationId,
        status,
        endReason,
        endedAt,
        npc,
        sceneCode,
        userTurnCount,
        feedback,
        completedMissions,
        followUp,
        reviewCardId);
  }

  public EndConversationResponse withFollowUp(FollowUpResponse followUp) {
    return new EndConversationResponse(
        conversationId,
        status,
        endReason,
        endedAt,
        npc,
        sceneCode,
        userTurnCount,
        feedback,
        completedMissions,
        followUp,
        reviewCardId);
  }
}
