package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.entity.LevelTestStatus;
import java.util.List;

/** ONB-03 레벨 테스트 시작 결과. 화면 04의 "문항 3 / N"은 {@code totalItems}로 그린다. */
public record LevelTestStartResponse(
    Long testId, LevelTestStatus status, int totalItems, List<LevelTestItemResponse> items) {}
