package com.elsehere.favor.dto;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.List;
import java.util.Map;

/**
 * FAV-03 NPC 말 걸기 요청. Unity가 플레이어 근처에서 지금 생활하는 NPC를 가까운 순서로 보낸다.
 *
 * @param candidates 말을 걸 수 있는 NPC. 가까운 순서
 */
public record FavorCallRequest(@NotNull @Size(max = 20) List<@Valid Candidate> candidates) {

  /**
   * 말 걸기 후보 한 명.
   *
   * @param needs 지금 그 NPC의 생활 욕구(CONV-01 needs와 같은 모양). 모르는 키는 버리고 0~100으로 자르며, 모르면 빈 맵이다
   */
  public record Candidate(@NotBlank String npcCode, Map<String, Double> needs) {

    public Candidate {
      Map<String, Double> known = NpcNeedValues.known(needs);
      needs = known == null ? Map.of() : known;
    }
  }
}
