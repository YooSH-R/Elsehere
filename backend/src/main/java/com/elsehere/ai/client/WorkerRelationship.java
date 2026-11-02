package com.elsehere.ai.client;

/**
 * 사용자와 NPC의 관계. NPC 대답(/v1/npc/reply)과 대화 뒤 관계 갱신(/v1/npc/relationship)에 보낸다.
 *
 * @param recognition 알아봄 0~100
 * @param affinity 호감 -100~100
 * @param daysSinceLastTalk 마지막 대화 뒤 지난 게임 속 날 수. 게임 하루 기준이 정해지기 전까지 0이다
 */
public record WorkerRelationship(int recognition, int affinity, int daysSinceLastTalk) {}
