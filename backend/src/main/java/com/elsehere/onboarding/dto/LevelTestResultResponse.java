package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.entity.LevelTestStatus;
import com.fasterxml.jackson.annotation.JsonInclude;

/**
 * ONB-05~07 레벨 테스트를 끝낸 결과.
 *
 * <p>점수와 결과 레벨은 완료(DONE)했을 때만 담는다. 그만하기(QUIT)·건너뛰기(SKIPPED)는 상태만 담는다.
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record LevelTestResultResponse(
    Long testId, LevelTestStatus status, Integer score, Integer resultLevel) {}
