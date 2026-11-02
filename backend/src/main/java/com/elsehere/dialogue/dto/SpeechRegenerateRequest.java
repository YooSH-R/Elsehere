package com.elsehere.conversation.dto;

import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/** CONV-07 NPC 음성 다시 만들기 요청. */
public record SpeechRegenerateRequest(@NotNull UUID requestId) {}
