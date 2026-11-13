package com.elsehere.review.dto;

import java.util.List;

/** 학습자와 NPC가 함께 쓰는, 복습 한 상황의 고정 정보. 본편 진행 상태와 분리한다. */
public record ReviewScenario(
    String key,
    String title,
    String situation,
    String learnerRole,
    List<String> learnerNotes,
    List<ReviewGoal> goals) {

  public ReviewScenario {
    goals = goals == null ? List.of() : List.copyOf(goals);
  }

  /** 판정할 조건이 없는 상황. */
  public ReviewScenario(
      String key, String title, String situation, String learnerRole, List<String> learnerNotes) {
    this(key, title, situation, learnerRole, learnerNotes, List.of());
  }
}
