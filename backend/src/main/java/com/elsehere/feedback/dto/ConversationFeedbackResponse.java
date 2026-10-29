package com.elsehere.feedback.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import java.util.List;

/**
 * FB-01 피드백 다시 보기. CONV-08 응답에 상세 평가를 더한다. AI를 호출하지 않는다.
 *
 * @param feedback 아직 만들지 않았으면 null
 * @param completedMissions 미션 연결 전까지 빈 목록이다
 */
public record ConversationFeedbackResponse(
    Long conversationId,
    NpcSummaryResponse npc,
    String sceneCode,
    long userTurnCount,
    FeedbackResponse feedback,
    List<Object> completedMissions,
    List<DetailedFeedbackItemResponse> detailedFeedback) {}
