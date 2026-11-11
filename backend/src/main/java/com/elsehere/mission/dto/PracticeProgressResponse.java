package com.elsehere.mission.dto;

import java.util.List;

/**
 * PRC-03 장소 연습 대화 하나의 진행. 대화를 끝내지 않고 NPC 대답마다 다시 읽는다.
 *
 * @param done 이 대화에서 상황의 목표를 모두 이뤘는지
 */
public record PracticeProgressResponse(String scenarioId, List<Goal> goals, boolean done) {

  public record Goal(String goalId, String title, String hint, boolean completed) {}
}
