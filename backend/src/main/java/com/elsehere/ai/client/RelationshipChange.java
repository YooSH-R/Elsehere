package com.elsehere.ai.client;

import java.util.List;

/**
 * 대화 뒤 관계 갱신(/v1/npc/relationship) 결과. 새 값이 아니라 저장값에 더할 변화량이다.
 *
 * @param events 이번 대화에서 센 사건(기록용)
 */
public record RelationshipChange(int recognitionDelta, int affinityDelta, List<String> events) {}
