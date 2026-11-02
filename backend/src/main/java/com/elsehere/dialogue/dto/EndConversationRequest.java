package com.elsehere.dialogue.dto;

import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/** CONV-08 대화 종료 요청. */
public record EndConversationRequest(@NotNull UUID requestId, boolean skipFeedback) {}
