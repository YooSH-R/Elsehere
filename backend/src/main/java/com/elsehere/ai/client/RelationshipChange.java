package com.elsehere.ai.client;

/**
 * 대화 뒤 관계 갱신(/v1/npc/relationship) 결과. 새 값이 아니라 저장값에 더할 알아봄 변화량이다. 호감은 대화 중에 발화마다 이미 바꿨으므로 오지 않는다.
 */
public record RelationshipChange(int recognitionDelta) {}
