package com.elsehere.user.entity;

/** 사용자 역할. 관리자(ADMIN)는 운영자 어드민(/admin)을 쓸 수 있다. 관리자는 운영자가 DB에서 직접 지정하며 서비스 안에서 바꾸는 기능은 두지 않는다. */
public enum UserRole {
  USER,
  ADMIN
}
