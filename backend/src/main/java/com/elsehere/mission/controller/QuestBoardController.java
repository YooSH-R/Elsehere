package com.elsehere.mission.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.service.QuestBoardService;
import com.elsehere.mission.service.QuestCancelService;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/missions/quests")
@RequiredArgsConstructor
public class QuestBoardController {
  private final QuestBoardService service;
  private final QuestCancelService cancels;

  @GetMapping
  public QuestBoardResponse board(@AuthenticationPrincipal LoginUser user) {
    return service.board(user.userId());
  }

  /** 하던 퀘스트를 그만둬 그 퀘스트의 처음으로 되돌리고, 바뀐 퀘스트 목록을 돌려준다. */
  @PostMapping("/{questId}/cancel")
  public QuestBoardResponse cancel(
      @AuthenticationPrincipal LoginUser user, @PathVariable String questId) {
    cancels.cancel(user.userId(), questId);
    return service.board(user.userId());
  }
}
