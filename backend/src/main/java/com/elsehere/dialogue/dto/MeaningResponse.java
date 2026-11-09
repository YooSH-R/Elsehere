package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.GlossaryResponse;
import java.util.List;

/**
 * CONV-04 NPC 대사 뜻 보기.
 *
 * @param glossary 대사의 내용어·숙어 풀이. 그런 말이 없으면 빈 목록
 */
public record MeaningResponse(Long turnId, String meaningKo, List<GlossaryResponse> glossary) {}
