package com.elsehere.world.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.world.dto.LastSceneRequest;
import com.elsehere.world.dto.LastSceneResponse;
import com.elsehere.world.service.LastSceneService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 장소를 옮길 때 재접속할 장소를 저장한다(WORLD-05). */
@RestController
@RequiredArgsConstructor
public class LastSceneController {

  private final LastSceneService lastSceneService;

  @PutMapping("/api/v1/me/last-scene")
  public LastSceneResponse saveLastScene(
      @AuthenticationPrincipal LoginUser loginUser, @Valid @RequestBody LastSceneRequest request) {
    return lastSceneService.saveLastScene(loginUser.userId(), request.sceneCode());
  }
}
