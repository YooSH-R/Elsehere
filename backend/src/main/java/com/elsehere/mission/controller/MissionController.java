package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.MissionListResponse;
import com.elsehere.mission.service.MissionService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

/** 오늘의 미션 조회와 자동 시작(MIS-01). */
@RestController
@RequiredArgsConstructor
public class MissionController {

  private final MissionService missionService;

  @GetMapping("/api/v1/missions")
  public MissionListResponse getTodayMissions(@AuthenticationPrincipal LoginUser loginUser) {
    return missionService.getTodayMissions(loginUser.userId());
  }
}
