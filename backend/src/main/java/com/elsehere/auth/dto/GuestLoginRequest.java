package com.elsehere.auth.dto;

import com.elsehere.user.entity.AvatarType;
import jakarta.validation.constraints.NotNull;

/** AUTH-07 게스트로 시작할 때 고른 캐릭터. */
public record GuestLoginRequest(@NotNull AvatarType avatarType) {}
