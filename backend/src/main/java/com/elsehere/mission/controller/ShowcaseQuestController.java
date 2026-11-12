package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.service.ShowcaseQuestService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** 기본값이 꺼진 시연 전용 경로. 실제 dev 배포 설정에서는 켜지 않는다. */
@RestController
@RequestMapping("/api/v1/missions/showcase")
@RequiredArgsConstructor
public class ShowcaseQuestController {
  private final ShowcaseQuestService service;

  @GetMapping
  public FirstDayMissionResponse status(@AuthenticationPrincipal LoginUser user) {
    return service.status(user.userId());
  }

  @PostMapping("/start")
  public FirstDayMissionResponse start(@AuthenticationPrincipal LoginUser user) {
    return service.start(user.userId());
  }

  @PostMapping("/reset")
  public FirstDayMissionResponse reset(@AuthenticationPrincipal LoginUser user) {
    return service.reset(user.userId());
  }

  @PostMapping("/stop")
  public FirstDayMissionResponse stop(@AuthenticationPrincipal LoginUser user) {
    return service.stop(user.userId());
  }

  @PostMapping("/taxi-arrival")
  public FirstDayMissionResponse taxiArrival(
      @AuthenticationPrincipal LoginUser user, @Valid @RequestBody ArrivalRequest request) {
    return service.taxiArrival(user.userId(), request.destination());
  }

  public record ArrivalRequest(@NotBlank @Size(max = 20) String destination) {}
}
