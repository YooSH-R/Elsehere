package com.elsehere.dialogue.dto;

import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/**
 * CONV-07 NPC 음성 다시 만들기 요청.
 *
 * @param speechSpeed 학습자가 고른 NPC 말 빠르기(0.8~1.5배). 없으면 기본값({@link NpcSpeechSpeed#DEFAULT})
 */
public record SpeechRegenerateRequest(
    @NotNull UUID requestId,
    @DecimalMin(NpcSpeechSpeed.MIN) @DecimalMax(NpcSpeechSpeed.MAX) Double speechSpeed) {

  public SpeechRegenerateRequest {
    speechSpeed = NpcSpeechSpeed.orDefault(speechSpeed);
  }

  public SpeechRegenerateRequest(UUID requestId) {
    this(requestId, null);
  }
}
