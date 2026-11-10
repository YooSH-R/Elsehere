package com.elsehere.onboarding.service;

/** AI 판정에 보낼 말하기 문항과 학습자가 말하거나 쓴 답. */
public record LevelTestSpeakingAnswer(LevelTestItem item, String answer) {}
