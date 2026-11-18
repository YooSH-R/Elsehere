package com.elsehere.favor.dto;

import java.util.List;
import java.util.Map;

/**
 * 돌발 부탁 소식 한 개(docs/npc-life-design.md 7.12). 턴 응답의 missionUpdates와 부탁 조회(FAV-01)·그만두기(FAV-02) 응답에
 * 쓴다.
 *
 * @param status OFFERED · ACCEPTED · GOT · DONE · DECLINED · EXPIRED · ABANDONED
 * @param titleKo 부탁 카드 제목. 예: Jake의 부탁 · 크루아상 사다 주기
 * @param fills 완료(DONE)했을 때만 부탁한 NPC가 채우는 욕구. 그 밖에는 null
 * @param expiresAtGameMinute 심부름 기한(1일차 0시부터 센 게임 분). 수락 전·대화 안 부탁은 null
 */
public record FavorResponse(
    Long favorId,
    String templateId,
    String status,
    String requesterNpcCode,
    String titleKo,
    List<StepResponse> steps,
    int rewardCoins,
    int affinityDelta,
    Map<String, Integer> fills,
    Long expiresAtGameMinute) {

  /** 부탁 카드의 단계 한 줄. npcCode가 있으면 그 NPC와, 없으면 그 장소의 직원과 말한다. */
  public record StepResponse(String placeCode, String npcCode, String textKo, boolean done) {}
}
