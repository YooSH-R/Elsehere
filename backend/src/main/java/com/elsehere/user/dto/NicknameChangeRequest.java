package com.elsehere.user.dto;

import com.elsehere.user.validation.Nickname;
import jakarta.validation.constraints.NotNull;

/**
 * USER-04 닉네임 변경 요청.
 *
 * @param nickname 바꿀 닉네임. 가입(AUTH-04)과 같은 닉네임 규칙을 따른다.
 */
public record NicknameChangeRequest(@NotNull @Nickname String nickname) {}
