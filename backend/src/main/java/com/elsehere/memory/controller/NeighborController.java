package com.elsehere.memory.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.memory.dto.NeighborListResponse;
import com.elsehere.memory.service.NeighborService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

/** 휴대폰 [이웃]의 NPC 목록(WORLD-08). */
@RestController
@RequiredArgsConstructor
public class NeighborController {

  private final NeighborService neighborService;

  @GetMapping("/api/v1/me/npcs")
  public NeighborListResponse neighbors(@AuthenticationPrincipal LoginUser loginUser) {
    return neighborService.neighbors(loginUser.userId());
  }
}
