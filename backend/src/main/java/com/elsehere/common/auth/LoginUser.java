package com.elsehere.common.auth;

import java.io.Serializable;

/**
 * 로그인한 사용자. 세션의 인증 정보에 담기며, 컨트롤러에서 {@code @AuthenticationPrincipal LoginUser}로 받는다.
 *
 * <p>세션에 저장되므로 직렬화할 수 있어야 한다.
 *
 * @param userId users.id
 */
public record LoginUser(Long userId) implements Serializable {}
