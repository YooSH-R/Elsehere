package com.elsehere.favor.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.service.FavorService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** 대화 중 돌발 부탁(docs/npc-life-design.md 7.12). 부탁이 생기고 바뀌는 것은 대화 턴 응답(missionUpdates)으로 알린다. */
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

  /** FAV-02 진행 중 부탁 그만두기. */
  @PostMapping("/{favorId}/abandon")
  public FavorResponse abandon(
      @AuthenticationPrincipal LoginUser user, @PathVariable Long favorId) {
    return service.abandon(user.userId(), favorId);
  }
}
