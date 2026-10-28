package com.elsehere.conversation.dto;

/**
 * NPC 대사의 단어 풀이 한 개.
 *
 * @param text 대사에 그대로 있는 말
 */
public record GlossaryResponse(String text, String meaningKo) {}
