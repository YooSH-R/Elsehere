package com.elsehere.admin.dto;

/**
 * AI 사용량 한 줄. 날짜·호출 종류·모델·사용자 구분 같은 기준 하나로 묶은 합계다.
 *
 * @param label 묶은 기준 값. 날짜는 한국 날짜(yyyy-MM-dd)
 * @param users 호출한 사용자 수
 * @param failed 워커 오류로 끝난 호출 수
 * @param audioMs 워커가 사용량으로 알려 준 음성 길이(음성 인식·합성 단계) 합
 */
public record AiUsageRow(
    String label,
    long users,
    long calls,
    long failed,
    long inputTokens,
    long outputTokens,
    long audioMs) {}
