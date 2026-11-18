package com.elsehere.favor.entity;

/** 부탁이 시작된 곳(docs/npc-life-design.md 7.6). 쉬는 간격을 따로 센다. */
public enum FavorOpening {
  /** 학습자가 건 대화 안. NPC가 내비친 욕구에 학습자가 반응했거나 주문 중 상황 사건이 생겼다. */
  TALK,
  /** NPC가 먼저 플레이어에게 말을 걸어 부탁했다. 다음 말 걸기까지 플레이어 기준 간격을 둔다. */
  CALL
}
