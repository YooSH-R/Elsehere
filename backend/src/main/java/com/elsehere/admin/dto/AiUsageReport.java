package com.elsehere.admin.dto;

import java.util.List;

/**
 * 어드민 AI 사용량 화면. 최근 {@code days}일 동안의 AI 호출 기록(ai_requests)을 묶어 보여 준다.
 *
 * @param byUserType 게스트(GUEST)와 가입자(MEMBER)
 * @param topUsers 입력·출력 토큰 합이 많은 사용자
 */
public record AiUsageReport(
    int days,
    AiUsageRow total,
    List<AiUsageRow> byDay,
    List<AiUsageRow> byKind,
    List<AiUsageRow> byModel,
    List<AiUsageRow> byUserType,
    List<AiUserUsage> topUsers) {}
