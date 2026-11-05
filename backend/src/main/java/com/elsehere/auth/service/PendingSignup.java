package com.elsehere.auth.service;

import com.elsehere.user.entity.AuthProvider;
import java.io.Serializable;

/**
 * Google 로그인(AUTH-02)을 마쳤지만 아직 가입(AUTH-04)하지 않은 제공자 계정. 세션에 둔다.
 *
 * @param guestUserId 게스트로 로그인한 세션에서 Google 로그인을 했으면 그 게스트 사용자 ID. 가입할 때 새 사용자 대신 이 게스트를 회원으로 바꾼다.
 *     게스트가 아니었으면 {@code null}.
 */
public record PendingSignup(
    AuthProvider provider, String providerUid, String email, Long guestUserId)
    implements Serializable {

  public static final String SESSION_ATTRIBUTE = "PENDING_SIGNUP";

  /** 게스트가 아닌 상태에서 Google 로그인한 가입 대기 정보. */
  public PendingSignup(AuthProvider provider, String providerUid, String email) {
    this(provider, providerUid, email, null);
  }
}
