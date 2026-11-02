package com.elsehere.conversation.dto;

import jakarta.validation.constraints.NotNull;
import java.util.UUID;

/** CONV-04 뜻 보기·CONV-05 힌트 요청. */
public record TurnHelpRequest(@NotNull UUID requestId) {}
