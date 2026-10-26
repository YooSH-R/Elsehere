package com.elsehere.world.dto;

import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.NpcType;

/**
 * 장소에 있는 NPC. persona·voice_id는 서버 내부 정보라 담지 않는다.
 *
 * @param roleLabel 이름표 아래에 보여주는 역할 이름(행인, 카페 직원)
 * @param canInitiate NPC가 먼저 말을 거는지
 * @param approachLine 먼저 말을 걸 때의 첫 말. 먼저 말을 걸지 않는 NPC는 {@code null}이다.
 */
public record NpcResponse(
    String npcCode,
    String name,
    NpcType npcType,
    String roleLabel,
    boolean canInitiate,
    String approachLine) {

  public static NpcResponse from(Npc npc) {
    return new NpcResponse(
        npc.getCode(),
        npc.getName(),
        npc.getNpcType(),
        npc.getRoleLabel(),
        npc.isCanInitiate(),
        npc.getApproachLine());
  }
}
