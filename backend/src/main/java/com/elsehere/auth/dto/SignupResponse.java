package com.elsehere.auth.dto;

import com.elsehere.user.dto.UserResponse;

/** 가입 결과(AUTH-04). 게스트로 시작하기(AUTH-07)도 같은 형식으로 응답한다. */
public record SignupResponse(UserResponse user) {}
