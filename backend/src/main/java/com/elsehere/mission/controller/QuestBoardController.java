package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.service.QuestBoardService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/missions/quests")
@RequiredArgsConstructor
public class QuestBoardController {
  private final QuestBoardService service;

  @GetMapping
  public QuestBoardResponse board(@AuthenticationPrincipal LoginUser user) {
    return service.board(user.userId());
  }
}
