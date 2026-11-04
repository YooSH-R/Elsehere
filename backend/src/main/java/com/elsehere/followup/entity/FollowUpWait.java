package com.elsehere.followup.entity;

/** 건넬 것이 준비되기까지의 정도(AI 워커 /v1/npc/follow-up의 wait). 게임이 지루하지 않도록 실제 시간으로 짧게 둔다. */
public enum FollowUpWait {
  QUICK(45),
  SHORT(90),
  LONG(180);

  private final long seconds;

  FollowUpWait(long seconds) {
    this.seconds = seconds;
  }

  public long seconds() {
    return seconds;
  }
}
