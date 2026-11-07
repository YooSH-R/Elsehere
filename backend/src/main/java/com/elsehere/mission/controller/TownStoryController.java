package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.service.TownStoryService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/missions/town-story")
@RequiredArgsConstructor
public class TownStoryController {
  private final TownStoryService service;

  @GetMapping
  public FirstDayMissionResponse status(@AuthenticationPrincipal LoginUser user) {
    return service.status(user.userId());
  }

  @PostMapping("/start")
  public FirstDayMissionResponse start(@AuthenticationPrincipal LoginUser user) {
    service.start(user.userId());
    return service.status(user.userId());
  }

  @PostMapping("/diner-meal")
  public FirstDayMissionResponse receiveDinerMeal(@AuthenticationPrincipal LoginUser user) {
    return service.receiveDinerMeal(user.userId());
  }
}
