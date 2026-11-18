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

/**
 * 시연 퀘스트 경로. 시연 전용 서버(설정 {@code elsehere.showcase.enabled})는 모든 계정이 시작 전부터 시연 상태이고, 그 밖의 서버는 퀘스트
 * 목록의 시연 카드를 눌러 {@code /start}한 계정만 진행하는 동안 시연 상태다.
 */
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
