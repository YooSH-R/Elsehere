package com.elsehere.conversation.dto;

import com.elsehere.conversation.entity.ConversationStatus;
import java.util.List;

/**
 * CONV-01 대화 시작 응답.
 *
 * @param runningMissions 이 NPC가 대상인 진행 중인 미션. 미션 연결 전까지 빈 목록이다
 * @param openingTurn NPC의 첫 대사
 */
public record ConversationStartResponse(
    Long conversationId,
    ConversationStatus status,
    int levelApplied,
    NpcSummaryResponse npc,
    List<Object> runningMissions,
    NpcTurnResponse openingTurn) {}
