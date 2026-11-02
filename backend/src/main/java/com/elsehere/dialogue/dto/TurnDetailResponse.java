package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.Speaker;
import java.util.List;

/**
 * 대화 기록의 한 턴(CONV-10·11). 저장된 결과만 담는다.
 *
 * @param speech NPC 턴만 담는다. 사용자 턴은 null
 */
public record TurnDetailResponse(
    Long turnId,
    int seq,
    Speaker speaker,
    String text,
    boolean rude,
    TranslationResponse translation,
    List<String> suggestWords,
    String meaningKo,
    String hintText,
    SpeechResponse speech) {}
