package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.entity.LevelTestStatus;

/** ONB-08에 담는 마지막 레벨 테스트. 클라이언트는 상태로 화면 04·05를 나눈다. */
public record LevelTestSummary(Long testId, LevelTestStatus status) {}
