package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.Speaker;

/**
 * 사용자 턴.
 *
 * @param rude 무례로 센 발화인지
 * @param translation 무례 판정과 관계없이 생성된 영어 번역을 담는다. 번역이 없으면 null
 */
public record UserTurnResponse(
    Long turnId,
    int seq,
    Speaker speaker,
    String text,
    boolean rude,
    TranslationResponse translation) {}
