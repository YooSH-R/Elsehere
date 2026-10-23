package com.elsehere.bootstrap.dto;

/** 접속한 사용자의 로그인 상태. 로그인하지 않았으면 BOOT-01이 401로 응답하므로 값이 없다. */
public enum AuthState {
  SIGNED_IN,
  SIGNUP_REQUIRED
}
