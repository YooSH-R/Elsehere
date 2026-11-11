package com.elsehere.dialogue.dto;

/**
 * CONV-05 힌트.
 *
 * @param hintText 학습자가 NPC에게 대답할 만한 영어 문장
 * @param hintMeaningKo 같은 문장의 한국어 뜻. 이전에 영어만 저장한 힌트면 null
 */
public record HintResponse(Long turnId, String hintText, String hintMeaningKo) {
  public HintResponse(Long turnId, String hintText) {
    this(turnId, hintText, null);
  }
}
