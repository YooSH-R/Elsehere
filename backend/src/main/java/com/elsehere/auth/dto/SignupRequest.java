package com.elsehere.auth.dto;

import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.validation.Nickname;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotNull;
import java.util.List;

/** AUTH-04 가입 정보. */
public record SignupRequest(
    @NotNull @Nickname String nickname,
    @NotNull AvatarType avatarType,
    @NotNull List<@Valid @NotNull ConsentAgreement> consents) {}
