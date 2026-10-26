package com.elsehere.world.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.world.dto.ObjectPrefRequest;
import com.elsehere.world.dto.ObjectPrefResponse;
import com.elsehere.world.service.ObjectPrefService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 사물 카드 자동 표시 끄기·다시 켜기(WORLD-03). */
@RestController
@RequiredArgsConstructor
public class ObjectPrefController {

  private final ObjectPrefService objectPrefService;

  @PutMapping("/api/v1/me/object-prefs/{objectKey}")
  public ObjectPrefResponse changeHoverHidden(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable String objectKey,
      @Valid @RequestBody ObjectPrefRequest request) {
    return objectPrefService.changeHoverHidden(
        loginUser.userId(), objectKey, request.hoverHidden());
  }
}
