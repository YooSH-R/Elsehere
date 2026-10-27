package com.elsehere.world.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.world.dto.WordPrefRequest;
import com.elsehere.world.dto.WordPrefResponse;
import com.elsehere.world.service.WordPrefService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 단어 카드 자동 표시 끄기·다시 켜기(WORLD-03). */
@RestController
@RequiredArgsConstructor
public class WordPrefController {

  private final WordPrefService wordPrefService;

  @PutMapping("/api/v1/me/word-prefs/{wordKey}")
  public WordPrefResponse changeHoverHidden(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable String wordKey,
      @Valid @RequestBody WordPrefRequest request) {
    return wordPrefService.changeHoverHidden(loginUser.userId(), wordKey, request.hoverHidden());
  }
}
