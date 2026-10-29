package com.elsehere.user.dto;

/**
 * USER-04 닉네임 변경 결과.
 *
 * @param nickname 저장한 닉네임. 다시 접속하면 BOOT-01의 {@code user.nickname}으로 내려간다.
 */
public record NicknameChangeResponse(String nickname) {}
