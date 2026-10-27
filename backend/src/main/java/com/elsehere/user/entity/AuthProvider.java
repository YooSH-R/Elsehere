package com.elsehere.user.entity;

/** 로그인 방식. 소셜 로그인은 Google만 쓰고, 게스트는 소셜 계정 없이 바로 시작한 사용자다. */
public enum AuthProvider {
  GOOGLE,
  GUEST
}
