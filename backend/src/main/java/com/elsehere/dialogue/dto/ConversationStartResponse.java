package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.ConversationStatus;
import java.util.List;

/**
 * CONV-01 대화 시작 응답.
 *
 * @param runningMissions 이 NPC가 대상인 진행 중인 미션. 미션 연결 전까지 빈 목록이다
 * @param openingTurn NPC의 첫 대사
 * @param conversation 발화 전송(CONV-03) 응답과 같은 형식의 대화 상태. 대화 화면이 처음부터 무례 횟수와 호감도를 보여 준다
 */
public record ConversationStartResponse(
    Long conversationId,
    ConversationStatus status,
    int levelApplied,
    NpcSummaryResponse npc,
    List<Object> runningMissions,
    NpcTurnResponse openingTurn,
    ConversationStateResponse conversation) {}
