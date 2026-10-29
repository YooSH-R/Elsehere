package com.elsehere.ai.entity;

/** AI 호출 상태. 한도 예약을 붙이기 전까지는 OK·FAILED만 쓴다. */
public enum AiRequestStatus {
  RESERVED,
  OK,
  FAILED,
  BLOCKED
}
