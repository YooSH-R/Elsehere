package com.elsehere.common.auth;

/**
 * 로그인한 사용자. 세션의 인증 정보에 담기며, 컨트롤러에서 {@code @AuthenticationPrincipal LoginUser}로 받는다.
 *
 * @param userId users.id
 */
public record LoginUser(Long userId) {}
