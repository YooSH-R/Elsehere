package com.elsehere.ai.client;

/**
 * NPC 대답 요청(/v1/npc/reply)의 기억. 저장된 최신 기억 카드를 다음 대화에 넘긴다.
 *
 * @param visits 사용자 발화가 있었던 대화를 끝낸 횟수. 1보다 작으면 워커가 기억을 쓰지 않는다
 * @param lastOrder 서버가 확인한 마지막 주문. 주문 기록이 없어 아직 null이다
 * @param card 기억 카드(name, claims, events, attitude, struggled). 없으면 null
 */
public record WorkerMemory(int visits, String lastOrder, Object card) {}
