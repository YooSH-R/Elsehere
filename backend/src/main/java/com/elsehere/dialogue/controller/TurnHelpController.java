package com.elsehere.dialogue.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.dialogue.dto.HintResponse;
import com.elsehere.dialogue.dto.MeaningResponse;
import com.elsehere.dialogue.dto.TurnHelpRequest;
import com.elsehere.dialogue.service.TurnHelpService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** NPC 대사 뜻 보기(CONV-04)와 힌트(CONV-05). */
@RestController
@RequiredArgsConstructor
public class TurnHelpController {

  private final TurnHelpService turnHelpService;

  @PostMapping("/api/v1/turns/{turnId}/meaning")
  public MeaningResponse meaning(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long turnId,
      @Valid @RequestBody TurnHelpRequest request) {
    return turnHelpService.meaning(loginUser.userId(), turnId, request.requestId());
  }

  @PostMapping("/api/v1/turns/{turnId}/hint")
  public HintResponse hint(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long turnId,
      @Valid @RequestBody TurnHelpRequest request) {
    return turnHelpService.hint(loginUser.userId(), turnId, request.requestId());
  }
}
