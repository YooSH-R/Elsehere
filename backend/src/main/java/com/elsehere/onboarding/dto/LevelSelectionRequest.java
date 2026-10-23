package com.elsehere.onboarding.dto;

import jakarta.validation.constraints.NotNull;

/** ONB-09 대화 난이도 설정 요청. */
public record LevelSelectionRequest(@NotNull Integer selectedLevel) {}
