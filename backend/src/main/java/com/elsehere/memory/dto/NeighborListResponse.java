package com.elsehere.memory.dto;

import java.util.List;

/**
 * WORLD-08 이웃 목록. 마을의 모든 NPC를 담고, 끝까지 마친 대화가 있는 NPC(이웃)가 먼저 온다.
 *
 * @param neighborCount 이웃으로 열린 NPC 수
 * @param totalCount 마을의 전체 NPC 수
 */
public record NeighborListResponse(
    int neighborCount, int totalCount, List<NeighborResponse> npcs) {}
