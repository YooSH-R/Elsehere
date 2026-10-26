package com.elsehere.world.dto;

import com.elsehere.world.entity.Scene;

/**
 * WORLD-01 장소 목록의 한 항목.
 *
 * @param unityKey 클라이언트가 불러올 Unity 씬 이름
 * @param isOpen 들어갈 수 있는 장소인지. 닫힌 장소는 화면에 "준비 중"으로 보인다.
 */
public record SceneSummaryResponse(String sceneCode, String name, String unityKey, boolean isOpen) {

  public static SceneSummaryResponse from(Scene scene) {
    return new SceneSummaryResponse(
        scene.getCode(), scene.getName(), scene.getUnityKey(), scene.isOpen());
  }
}
