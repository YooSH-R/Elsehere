package com.elsehere.dialogue.dto;

import java.util.List;

/**
 * CONV-12 추천 단어와 미리 만든 문장·뜻. 문장 생성만 실패하면 문장·뜻은 null이다.
 *
 * @param suggestWords 학습자가 NPC에게 대답할 때 쓸 만한 단어·짧은 구
 */
public record SuggestWordsResponse(
    Long turnId, List<String> suggestWords, String hintText, String hintMeaningKo) {
  public SuggestWordsResponse(Long turnId, List<String> suggestWords) {
    this(turnId, suggestWords, null, null);
  }
}
