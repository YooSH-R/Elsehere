package com.elsehere.onboarding.dto;

import jakarta.validation.constraints.NotBlank;

/** ONB-04 문항 응답. 선택형 문항은 고른 보기 문자열을 그대로 보낸다. */
public record LevelTestAnswerRequest(@NotBlank String answer) {}
