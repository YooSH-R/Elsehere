package com.elsehere.favor.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.favor.dto.FavorCallRequest;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.service.FavorService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * 돌발 부탁(docs/npc-life-design.md 7.12). 대화 속 부탁이 생기고 바뀌는 것은 대화 턴 응답(missionUpdates)으로 알리고, NPC 말 걸기는
 * Unity가 묻는다(FAV-03).
 */
@RestController
@RequestMapping("/api/v1/favors")
@RequiredArgsConstructor
public class FavorController {

  private final FavorService service;

  /** FAV-01 진행 중 부탁. 다시 접속했을 때 부탁 카드를 복원한다. 없으면 204. */
  @GetMapping("/current")
  public ResponseEntity<FavorResponse> current(@AuthenticationPrincipal LoginUser user) {
    return service
        .current(user.userId())
        .map(ResponseEntity::ok)
        .orElseGet(() -> ResponseEntity.noContent().build());
  }

  /**
   * FAV-03 NPC 말 걸기. 플레이어 기준 간격이 지났고 근처 NPC 가운데 꺼낼 부탁이 있으면 그 부탁을 저장해 돌려준다. Unity는 그 NPC를 보내 말을 건다.
   * 말을 걸 때가 아니면 204.
   */
  @PostMapping("/calls")
  public ResponseEntity<FavorResponse> call(
      @AuthenticationPrincipal LoginUser user, @Valid @RequestBody FavorCallRequest request) {
    return service
        .call(user.userId(), request.candidates())
        .map(ResponseEntity::ok)
        .orElseGet(() -> ResponseEntity.noContent().build());
  }

  /** FAV-04 말을 걸어 온 NPC의 제안을 거절하거나 무시했다. */
  @PostMapping("/{favorId}/decline")
  public FavorResponse decline(
      @AuthenticationPrincipal LoginUser user, @PathVariable Long favorId) {
    return service.decline(user.userId(), favorId);
  }

  /** FAV-02 진행 중 부탁 그만두기. */
  @PostMapping("/{favorId}/abandon")
  public FavorResponse abandon(
      @AuthenticationPrincipal LoginUser user, @PathVariable Long favorId) {
    return service.abandon(user.userId(), favorId);
  }
}
