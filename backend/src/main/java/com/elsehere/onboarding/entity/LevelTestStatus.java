package com.elsehere.onboarding.entity;

/** 레벨 테스트 상태. 진행 중(IN_PROGRESS)에서 완료(DONE)·그만하기(QUIT)로 끝나고, 건너뛰기(SKIPPED)는 처음부터 끝난 기록이다. */
public enum LevelTestStatus {
  IN_PROGRESS,
  DONE,
  QUIT,
  SKIPPED
}
