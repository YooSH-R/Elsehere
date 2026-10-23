package com.elsehere.onboarding.dto;

import com.elsehere.user.entity.OnboardingStep;

/** ONB-09 대화 난이도 설정 결과. */
public record LevelSelectionResponse(
    String languageCode, int selectedLevel, OnboardingStep onboardingStep) {}
