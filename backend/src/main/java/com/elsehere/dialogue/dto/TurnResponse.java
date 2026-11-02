package com.elsehere.conversation.dto;

import java.util.List;

/**
 * CONV-03 발화 전송 응답.
 *
 * @param missionUpdates 이 턴으로 확정된 미션. 미션 판정을 붙이기 전까지 빈 목록이다
 */
public record TurnResponse(
    UserTurnResponse userTurn,
    NpcTurnResponse npcTurn,
    ConversationStateResponse conversation,
    List<Object> missionUpdates) {}
