package com.elsehere.auth.dto;

import com.elsehere.user.dto.UserResponse;

/** AUTH-04 가입 결과. */
public record SignupResponse(UserResponse user) {}
