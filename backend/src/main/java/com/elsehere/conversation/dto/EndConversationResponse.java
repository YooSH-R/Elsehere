package com.elsehere.conversation.dto;

import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import java.time.OffsetDateTime;
import java.util.List;

/**
 * CONV-08 대화 종료 응답.
 *
 * @param feedback 짧은 피드백. 피드백 생성을 붙이기 전까지 null이다
 * @param completedMissions 이 대화에서 완료한 미션. 미션 연결 전까지 빈 목록이다
 */
public record EndConversationResponse(
    Long conversationId,
    ConversationStatus status,
    ConversationEndReason endReason,
    OffsetDateTime endedAt,
    NpcSummaryResponse npc,
    String sceneCode,
    long userTurnCount,
    Object feedback,
    List<Object> completedMissions) {}
