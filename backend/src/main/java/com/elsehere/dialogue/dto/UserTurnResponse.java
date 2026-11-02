package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.Speaker;

/**
 * 사용자 턴.
 *
 * @param rude 무례로 센 발화인지
 * @param translation 번역이 있고 무례가 아니라고 확정된 턴에만 담는다. 그 밖에는 null
 */
public record UserTurnResponse(
    Long turnId,
    int seq,
    Speaker speaker,
    String text,
    boolean rude,
    TranslationResponse translation) {}
