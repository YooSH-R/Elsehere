package com.elsehere.world.dto;

import jakarta.validation.constraints.NotBlank;

/**
 * WORLD-05 현재 장소 저장 요청.
 *
 * @param sceneCode 옮겨 간 장소 코드(scenes.code)
 */
public record LastSceneRequest(@NotBlank String sceneCode) {}
