package com.elsehere.dialogue.dto;

import java.util.List;

/**
 * CONV-03 발화 전송 응답.
 *
 * @param missionUpdates 이 턴으로 확정된 미션. 미션 판정을 붙이기 전까지 빈 목록이다
 * @param policeDispatch 이 턴에서 경찰관이 신고를 받아 순찰차가 출동하면 출동할 곳. 출동하지 않으면 null
 */
public record TurnResponse(
    UserTurnResponse userTurn,
    NpcTurnResponse npcTurn,
    ConversationStateResponse conversation,
    List<Object> missionUpdates,
    PoliceDispatchResponse policeDispatch) {

  public TurnResponse(
      UserTurnResponse userTurn,
      NpcTurnResponse npcTurn,
      ConversationStateResponse conversation,
      List<Object> missionUpdates) {
    this(userTurn, npcTurn, conversation, missionUpdates, null);
  }
}
