package com.elsehere.world.dto;

import java.util.List;

/**
 * WORLD-02 장소에 들어갈 때 받는 NPC와 사물 카드.
 *
 * @param unityKey 클라이언트가 불러올 Unity 씬 이름
 * @param isOpen 들어갈 수 있는 장소인지
 */
public record SceneDetailResponse(
    String sceneCode,
    String name,
    String unityKey,
    boolean isOpen,
    List<NpcResponse> npcs,
    List<ObjectCardResponse> objects) {}
