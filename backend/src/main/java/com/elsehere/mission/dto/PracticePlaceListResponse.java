package com.elsehere.mission.dto;

import java.util.List;

/** PRC-01 장소 연습 목록. 장소는 정한 순서, 상황은 번호 순서다. */
public record PracticePlaceListResponse(List<Place> places) {

  /**
   * @param summary 목록 아래 줄. 앞의 두 상황 제목을 " · "로 잇는다
   * @param doneCount 이 사용자가 한 번이라도 끝낸 상황 수
   */
  public record Place(
      String placeCode,
      String name,
      String summary,
      String npcCode,
      String npcName,
      String roleLabel,
      String sceneCode,
      int doneCount,
      int totalCount,
      List<Scenario> scenarios) {}

  /**
   * @param done 이 사용자가 이 상황의 목표를 모두 이룬 적이 있는지
   */
  public record Scenario(
      String scenarioId,
      int number,
      String title,
      String description,
      boolean done,
      List<Goal> goals) {}

  public record Goal(String goalId, String title, String hint) {}
}
