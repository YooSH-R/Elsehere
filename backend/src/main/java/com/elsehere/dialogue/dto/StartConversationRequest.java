package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.InitiatedBy;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/**
 * CONV-01 대화 시작 요청.
 *
 * @param npcOffDuty NPC가 근무 시간이 아닌지(교대 밖). 없거나 null이면 근무 중으로 보아, 이 칸을 보내지 않는 클라이언트는 지금처럼 응대 상황으로
 *     대화한다
 */
public record StartConversationRequest(
    @NotNull UUID requestId,
    @NotBlank String npcCode,
    @NotNull InitiatedBy initiatedBy,
    Boolean npcOffDuty) {

  public StartConversationRequest {
    npcOffDuty = Boolean.TRUE.equals(npcOffDuty);
  }

  /** 근무 중인 NPC와의 대화. */
  public StartConversationRequest(UUID requestId, String npcCode, InitiatedBy initiatedBy) {
    this(requestId, npcCode, initiatedBy, false);
  }
}
