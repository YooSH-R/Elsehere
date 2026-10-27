package com.elsehere.world.dto;

/**
 * WORLD-05 현재 장소 저장 결과.
 *
 * @param sceneCode 저장한 장소 코드. 재접속하면 BOOT-01의 {@code lastSceneCode}로 내려간다.
 */
public record LastSceneResponse(String sceneCode) {}
