package com.elsehere.admin.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/**
 * 어드민 로그인.
 *
 * @param loginId 어드민 아이디
 * @param password 어드민 비밀번호. 로그에 남기지 않는다.
 */
public record AdminLoginRequest(
    @NotBlank @Size(max = 100) String loginId, @NotBlank @Size(max = 200) String password) {}
