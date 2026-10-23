package com.elsehere.auth.dto;

import com.elsehere.user.entity.AvatarType;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.List;

/** AUTH-04 가입 정보. */
public record SignupRequest(
    @NotBlank @Size(max = 30) String nickname,
    @NotNull AvatarType avatarType,
    @NotNull List<@Valid @NotNull ConsentAgreement> consents) {}
