package com.elsehere.dialogue.dto;

import com.elsehere.conversation.dto.GlossaryResponse;
import java.util.List;

/**
 * CONV-04 NPC 대사 뜻 보기.
 *
 * @param glossary 학습자가 모를 만한 말의 풀이. 쉬운 대사면 빈 목록
 */
public record MeaningResponse(Long turnId, String meaningKo, List<GlossaryResponse> glossary) {}
