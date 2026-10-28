package com.elsehere.onboarding.dto;

import jakarta.validation.constraints.NotBlank;

/** ONB-03 레벨 테스트 시작과 ONB-07 건너뛰기 요청. 어느 학습 언어의 테스트인지 담는다. */
public record LevelTestLanguageRequest(@NotBlank String languageCode) {}
