package com.elsehere.conversation.dto;

import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;

/** 턴을 처리한 뒤의 대화 상태. */
public record ConversationStateResponse(
    ConversationStatus status, ConversationEndReason endReason, int rudeCount, int rudeLimit) {}
