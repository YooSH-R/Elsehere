package com.elsehere.world.dto;

import jakarta.validation.constraints.NotNull;

/**
 * WORLD-03 사물 카드 자동 표시 설정 요청.
 *
 * @param hoverHidden true면 자동 표시를 끄고, false면 다시 켠다
 */
public record ObjectPrefRequest(@NotNull Boolean hoverHidden) {}
