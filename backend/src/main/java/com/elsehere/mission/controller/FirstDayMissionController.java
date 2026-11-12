package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.ShowcaseQuestService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/missions/first-day")
@RequiredArgsConstructor
public class FirstDayMissionController {
  private final FirstDayMissionService service;
  private final ShowcaseQuestService showcase;

  @GetMapping
  public FirstDayMissionResponse status(@AuthenticationPrincipal LoginUser user) {
    return showcase.activeStatus(user.userId()).orElseGet(() -> service.status(user.userId()));
  }

  @PostMapping("/start")
  public FirstDayMissionResponse start(@AuthenticationPrincipal LoginUser user) {
    return service.start(user.userId());
  }

  @PostMapping("/visit")
  public FirstDayMissionResponse visit(
      @AuthenticationPrincipal LoginUser user, @Valid @RequestBody VisitRequest request) {
    return showcase
        .visitIfActive(user.userId(), request.scene())
        .orElseGet(() -> service.visit(user.userId(), request.scene()));
  }

  @PostMapping("/rest")
  public FirstDayMissionResponse rest(@AuthenticationPrincipal LoginUser user) {
    return service.rest(user.userId());
  }

  public record VisitRequest(@NotBlank String scene) {}
}
