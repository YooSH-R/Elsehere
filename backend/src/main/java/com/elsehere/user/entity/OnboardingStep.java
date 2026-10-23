package com.elsehere.user.entity;

/** 초기 설정 진행 단계. LANG → LEVEL → TUTORIAL → DONE 순서로 진행하며, 튜토리얼을 마치거나 건너뛰면 DONE이다. */
public enum OnboardingStep {
  LANG,
  LEVEL,
  TUTORIAL,
  DONE
}
