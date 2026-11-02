package com.elsehere.dialogue.dto;

/**
 * CONV-05 힌트.
 *
 * @param hintText 학습자가 NPC에게 대답할 만한 영어 문장
 */
public record HintResponse(Long turnId, String hintText) {}
