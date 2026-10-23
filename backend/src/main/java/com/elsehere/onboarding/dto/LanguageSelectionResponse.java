package com.elsehere.onboarding.dto;

import com.elsehere.user.entity.OnboardingStep;

/** ONB-02 학습 언어 선택 결과. */
public record LanguageSelectionResponse(String languageCode, OnboardingStep onboardingStep) {}
