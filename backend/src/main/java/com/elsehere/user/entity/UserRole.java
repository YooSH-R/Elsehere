package com.elsehere.user.entity;

/** 사용자 역할. 관리자(ADMIN)는 운영자 어드민(/admin)을 쓸 수 있다. 관리자는 다른 관리자가 어드민의 관리자 화면에서 지정·해제한다. */
public enum UserRole {
  USER,
  ADMIN
}
