package com.elsehere.mission.dto;

import java.util.List;

/** 퀘스트 목록과 끝낸 퀘스트의 보상까지 더한 코인 잔액. */
public record QuestBoardResponse(int balance, List<Quest> quests) {
  /**
   * @param status LOCKED(앞 퀘스트를 아직 끝내지 않음)·AVAILABLE(시작할 수 있음)·IN_PROGRESS·DONE
   */
  public record Quest(
      String id, String title, String description, int reward, String status, List<Stop> stops) {}

  /**
   * 퀘스트에서 들르는 곳 하나(진행 단계 하나).
   *
   * @param todo 그곳에서 할 일
   */
  public record Stop(
      String place,
      String npcName,
      String scene,
      String npcCode,
      String todo,
      boolean done,
      boolean current) {}
}
