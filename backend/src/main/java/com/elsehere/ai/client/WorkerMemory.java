package com.elsehere.ai.client;

import com.fasterxml.jackson.annotation.JsonIgnore;

/**
 * NPC 대답 요청(/v1/npc/reply)의 기억. 저장된 최신 기억 카드를 다음 대화에 넘긴다. 학습자가 요청하고 한 일(주문·길 묻기 등)은 카드의 {@code
 * requests}에 워커가 적는다.
 *
 * @param visits 사용자 발화가 있었던 대화를 끝낸 횟수. 1보다 작으면 워커가 기억을 쓰지 않는다
 * @param card 기억 카드(name, claims, events, attitude, struggled, requests). 없으면 null
 * @param relationship 알아봄·호감. 요청의 {@code memory} 안이 아니라 최상위 {@code relationship}으로 보낸다
 */
public record WorkerMemory(int visits, Object card, @JsonIgnore WorkerRelationship relationship) {

  public WorkerMemory(int visits, Object card) {
    this(visits, card, null);
  }
}
