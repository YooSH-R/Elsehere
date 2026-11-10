package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.InputType;
import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.UUID;

/**
 * CONV-03 발화 전송 요청.
 *
 * @param text 사용자가 보낸 최종 문장. 음성이면 전사 결과를 그대로 보낸다
 * @param sttRawText 음성 입력의 전사 원본. 텍스트 입력이면 null
 * @param speechSpeed 학습자가 고른 NPC 말 빠르기(0.8~1.5배). 없으면 기본값({@link NpcSpeechSpeed#DEFAULT})
 */
public record TurnRequest(
    @NotNull UUID requestId,
    @NotNull InputType inputType,
    @NotBlank @Size(max = 500) String text,
    @Size(max = 500) String sttRawText,
    @DecimalMin(NpcSpeechSpeed.MIN) @DecimalMax(NpcSpeechSpeed.MAX) Double speechSpeed) {

  public TurnRequest {
    speechSpeed = NpcSpeechSpeed.orDefault(speechSpeed);
  }

  public TurnRequest(UUID requestId, InputType inputType, String text, String sttRawText) {
    this(requestId, inputType, text, sttRawText, null);
  }
}
