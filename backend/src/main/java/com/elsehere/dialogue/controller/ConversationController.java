package com.elsehere.dialogue.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.dialogue.dto.ConversationDetailResponse;
import com.elsehere.dialogue.dto.ConversationListResponse;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.dialogue.service.ConversationHistoryService;
import com.elsehere.dialogue.service.ConversationService;
import com.elsehere.dialogue.service.TurnService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/**
 * NPC 자유 대화: 시작(CONV-01), 발화(CONV-03), 종료(CONV-08), 지난 대화 목록(CONV-09), 상세(CONV-10), 진행 중인
 * 대화(CONV-11).
 */
@RestController
@RequiredArgsConstructor
public class ConversationController {

  private final ConversationService conversationService;
  private final TurnService turnService;
  private final ConversationHistoryService conversationHistoryService;

  @PostMapping("/api/v1/conversations")
  @ResponseStatus(HttpStatus.CREATED)
  public ConversationStartResponse start(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody StartConversationRequest request) {
    return conversationService.start(loginUser.userId(), request);
  }

  @PostMapping("/api/v1/conversations/{conversationId}/turns")
  public TurnResponse sendTurn(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody TurnRequest request) {
    return turnService.send(loginUser.userId(), conversationId, request);
  }

  @PostMapping("/api/v1/conversations/{conversationId}/end")
  public EndConversationResponse end(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody EndConversationRequest request) {
    return conversationService.end(loginUser.userId(), conversationId, request);
  }

  @GetMapping("/api/v1/conversations")
  public ConversationListResponse list(
      @AuthenticationPrincipal LoginUser loginUser,
      @RequestParam(required = false) String npcCode,
      @RequestParam(required = false) String cursor,
      @RequestParam(defaultValue = "20") int size) {
    return conversationHistoryService.list(loginUser.userId(), npcCode, cursor, size);
  }

  @GetMapping("/api/v1/conversations/active")
  public ResponseEntity<ConversationDetailResponse> active(
      @AuthenticationPrincipal LoginUser loginUser) {
    return conversationService
        .active(loginUser.userId())
        .map(ResponseEntity::ok)
        .orElseGet(() -> ResponseEntity.noContent().build());
  }

  @GetMapping("/api/v1/conversations/{conversationId}")
  public ConversationDetailResponse detail(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long conversationId) {
    return conversationService.detail(loginUser.userId(), conversationId);
  }
}
