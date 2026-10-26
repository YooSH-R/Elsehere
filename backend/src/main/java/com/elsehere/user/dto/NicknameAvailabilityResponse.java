package com.elsehere.user.dto;

/** AUTH-06 닉네임 사용 가능 여부. */
public record NicknameAvailabilityResponse(String nickname, boolean available) {}
