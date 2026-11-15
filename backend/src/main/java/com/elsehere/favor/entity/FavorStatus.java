package com.elsehere.favor.entity;

import java.util.EnumSet;
import java.util.Set;

/** 돌발 부탁의 진행 상태(docs/npc-life-design.md 7.7). */
public enum FavorStatus {
  /** NPC가 대답에서 부탁을 꺼냈다. 심부름이면 수락을, 대화 안 부탁이면 목표를 기다린다. */
  OFFERED,
  /** 심부름을 수락해 해결처로 가는 중이다. */
  ACCEPTED,
  /** 해결처에서 물건을 받거나 일을 마치고 부탁한 NPC에게 돌아가는 중이다. */
  GOT,
  DONE,
  DECLINED,
  EXPIRED,
  ABANDONED;

  public static final Set<FavorStatus> ACTIVE = EnumSet.of(OFFERED, ACCEPTED, GOT);

  public boolean active() {
    return ACTIVE.contains(this);
  }
}
