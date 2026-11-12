package com.elsehere.admin.dto;

/**
 * 사용자 한 명의 AI 사용량.
 *
 * @param name 화면에 보여 줄 이름({@link AdminUserName}). 가입자는 이메일이다
 */
public record AiUserUsage(
    Long userId,
    boolean guest,
    String name,
    long calls,
    long failed,
    long inputTokens,
    long outputTokens) {}
