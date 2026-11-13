package com.elsehere.admin.service;

import java.io.Serializable;

/**
 * 로그인한 어드민. 세션의 어드민 인증 정보에 담기며, 게임 사용자({@code LoginUser})와 섞이지 않게 따로 둔다.
 *
 * <p>세션에 저장되므로 직렬화할 수 있어야 한다.
 *
 * @param loginId 어드민 아이디
 */
public record AdminLogin(String loginId) implements Serializable {}
