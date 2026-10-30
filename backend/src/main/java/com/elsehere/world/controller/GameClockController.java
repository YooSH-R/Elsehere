package com.elsehere.world.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.world.dto.GameClockResponse;
import com.elsehere.world.service.GameClockService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
public class GameClockController {

  private final GameClockService gameClockService;

  @GetMapping("/api/v1/me/game-clock")
  public GameClockResponse now(@AuthenticationPrincipal LoginUser loginUser) {
    return gameClockService.now(loginUser.userId());
  }

  @PostMapping("/api/v1/me/game-clock/beats")
  public GameClockResponse beat(@AuthenticationPrincipal LoginUser loginUser) {
    return gameClockService.beat(loginUser.userId());
  }
}
