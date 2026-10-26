package com.elsehere.world.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.world.dto.SceneDetailResponse;
import com.elsehere.world.dto.SceneSummaryResponse;
import com.elsehere.world.service.SceneService;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RestController;

/** 장소 목록(WORLD-01)과 장소의 NPC·사물 카드(WORLD-02). */
@RestController
@RequiredArgsConstructor
public class SceneController {

  private final SceneService sceneService;

  @GetMapping("/api/v1/scenes")
  public List<SceneSummaryResponse> getScenes() {
    return sceneService.getScenes();
  }

  @GetMapping("/api/v1/scenes/{sceneCode}")
  public SceneDetailResponse getScene(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable String sceneCode) {
    return sceneService.getScene(sceneCode, loginUser.userId());
  }
}
