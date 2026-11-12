package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.Speaker;
import java.util.List;

/**
 * 사용자 턴.
 *
 * @param rude 무례로 센 발화인지
 * @param translation 무례 판정과 관계없이 생성된 영어 번역·교정문을 담는다. 표시할 표현이 없으면 null
 * @param betterExpressions 표현 카드에 접어 둘 같은 뜻의 다른 표현(최대 3개). 고친 문장·한국어가 섞인 말에도 준다. 없으면 빈 목록
 */
public record UserTurnResponse(
    Long turnId,
    int seq,
    Speaker speaker,
    String text,
    boolean rude,
    TranslationResponse translation,
    List<String> betterExpressions) {}
