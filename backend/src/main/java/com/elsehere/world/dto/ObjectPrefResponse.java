package com.elsehere.world.dto;

/**
 * WORLD-03 사물 카드 자동 표시 설정 결과.
 *
 * @param objectKey Unity 씬의 사물 키(world_objects.unity_key)
 * @param hoverHidden 자동 표시를 껐는지. 끈 사물은 호버로 카드를 띄우지 않고 클릭해야 보인다.
 */
public record ObjectPrefResponse(String objectKey, boolean hoverHidden) {}
