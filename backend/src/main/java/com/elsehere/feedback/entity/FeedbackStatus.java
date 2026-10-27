package com.elsehere.feedback.entity;

/** 짧은 피드백 상태. 평가할 사용자 발화가 없거나 사용자가 건너뛰면 SKIPPED다. */
public enum FeedbackStatus {
  DONE,
  FAILED,
  SKIPPED
}
