package com.elsehere.admin.dto;

import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/**
 * 관리자 추가.
 *
 * @param email 관리자로 지정할 사람이 게임에 가입한 Google 이메일. 대소문자는 가리지 않는다.
 */
public record AdminGrantRequest(@NotBlank @Email @Size(max = 255) String email) {}
