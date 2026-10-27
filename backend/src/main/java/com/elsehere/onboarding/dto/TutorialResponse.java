package com.elsehere.onboarding.dto;

import com.elsehere.user.entity.OnboardingStep;

/** TUT-01·02 튜토리얼 완료·건너뛰기 결과. */
public record TutorialResponse(OnboardingStep onboardingStep) {}
