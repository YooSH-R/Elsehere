package com.elsehere.auth.dto;

import com.elsehere.user.entity.ConsentType;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

/** 사용자가 동의한 약관과 그 버전. */
public record ConsentAgreement(@NotNull ConsentType consentType, @NotBlank String version) {}
