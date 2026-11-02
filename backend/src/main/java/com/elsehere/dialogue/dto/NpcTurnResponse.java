package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.Speaker;
import java.util.List;

/**
 * NPC 턴.
 *
 * @param suggestWords 추천 단어. 만들지 않았으면 null
 */
public record NpcTurnResponse(
    Long turnId,
    int seq,
    Speaker speaker,
    String text,
    List<String> suggestWords,
    SpeechResponse speech) {}
