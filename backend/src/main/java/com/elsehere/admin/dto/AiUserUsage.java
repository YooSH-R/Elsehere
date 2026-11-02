package com.elsehere.admin.dto;

/** 사용자 한 명의 AI 사용량. 개인정보 없이 사용자 ID와 게스트 여부만 보여 준다. */
public record AiUserUsage(
    Long userId, boolean guest, long calls, long failed, long inputTokens, long outputTokens) {}
