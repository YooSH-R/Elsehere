package com.elsehere.conversation.dto;

import com.elsehere.conversation.entity.InitiatedBy;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/** CONV-01 대화 시작 요청. */
public record StartConversationRequest(
    @NotNull UUID requestId, @NotBlank String npcCode, @NotNull InitiatedBy initiatedBy) {}
