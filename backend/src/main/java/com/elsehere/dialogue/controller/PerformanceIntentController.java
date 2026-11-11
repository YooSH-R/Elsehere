package com.elsehere.dialogue.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.dialogue.service.PerformanceIntentService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
public class PerformanceIntentController {
  private final PerformanceIntentService service;

  public record Request(@NotNull UUID requestId, @NotBlank @Size(max = 500) String text) {}

  public record Response(String intent) {}

  @PostMapping("/api/v1/conversations/{conversationId}/performance-intent")
  public Response classify(
      @AuthenticationPrincipal LoginUser user,
      @PathVariable Long conversationId,
      @Valid @RequestBody Request request) {
    return new Response(
        service.classify(user.userId(), conversationId, request.requestId(), request.text()));
  }
}
