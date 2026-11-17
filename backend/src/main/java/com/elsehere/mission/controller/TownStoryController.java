package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.service.ShowcaseQuestService;
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
  private final ShowcaseQuestService showcase;

  @GetMapping
  public FirstDayMissionResponse status(@AuthenticationPrincipal LoginUser user) {
    return service.status(user.userId());
  }

  @PostMapping("/start")
  public FirstDayMissionResponse start(@AuthenticationPrincipal LoginUser user) {
    service.start(user.userId());
    // 새 퀘스트를 시작하면 하다 멈춘 시연은 취소한다.
    showcase.cancelUnfinished(user.userId());
    return service.status(user.userId());
  }

  @PostMapping("/diner-meal")
  public FirstDayMissionResponse receiveDinerMeal(@AuthenticationPrincipal LoginUser user) {
    return service.receiveDinerMeal(user.userId());
  }
}
