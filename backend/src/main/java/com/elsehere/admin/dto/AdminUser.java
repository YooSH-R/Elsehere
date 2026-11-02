package com.elsehere.admin.dto;

/**
 * 어드민 관리자 목록의 한 사람.
 *
 * @param userId 사용자 ID. 해제 API 주소에 쓴다.
 * @param nickname 게임 닉네임
 * @param email 가입한 Google 이메일
 */
public record AdminUser(Long userId, String nickname, String email) {}
