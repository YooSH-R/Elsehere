package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.world.entity.PlaceGroup;
import java.time.OffsetDateTime;

/**
 * 지난 대화 목록(CONV-09)의 대화 하나.
 *
 * @param placeName NPC가 일하는 곳(카페, 우체국). 근무 시간 밖에 만난 대화는 null
 * @param placeGroup 대화 노트 장소 탭의 묶음. NPC의 실제 근무지로 정하고, 근무 시간 밖에 만난 대화는 일상(DAILY)
 * @param userTurnCount 사용자가 보낸 발화 수
 * @param feedbackStatus 짧은 피드백 상태. 피드백을 만들기 전(진행 중인 대화 등)이면 null
 * @param practiceCount 이 대화로 다시 연습한 횟수. 연습하지 않았으면 0
 */
public record ConversationSummaryResponse(
    Long conversationId,
    NpcSummaryResponse npc,
    String sceneCode,
    String placeName,
    PlaceGroup placeGroup,
    ConversationStatus status,
    ConversationEndReason endReason,
    OffsetDateTime startedAt,
    OffsetDateTime endedAt,
    int userTurnCount,
    FeedbackStatus feedbackStatus,
    boolean feedbackUnread,
    int practiceCount) {

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
        null,
        null,
        status,
        endReason,
        startedAt,
        endedAt,
        userTurnCount,
        feedbackStatus,
        false,
        0);
  }
}
