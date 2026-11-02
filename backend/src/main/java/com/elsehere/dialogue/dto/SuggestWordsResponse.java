package com.elsehere.dialogue.dto;

import java.util.List;

/**
 * CONV-12 추천 단어.
 *
 * @param suggestWords 학습자가 NPC에게 대답할 때 쓸 만한 단어·짧은 구
 */
public record SuggestWordsResponse(Long turnId, List<String> suggestWords) {}
