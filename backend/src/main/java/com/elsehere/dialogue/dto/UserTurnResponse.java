package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.Speaker;
import java.util.List;

/**
 * 사용자 턴.
 *
 * @param rude 무례로 센 발화인지
 * @param translation 무례 판정과 관계없이 생성된 영어 번역·교정문을 담는다. 표시할 표현이 없으면 null
 * @param betterExpressions 틀리진 않았지만 어색하거나 짧은 영어에 보여 줄 같은 뜻의 더 좋은 문장(최대 3개). 없으면 빈 목록
 */
public record UserTurnResponse(
    Long turnId,
    int seq,
    Speaker speaker,
    String text,
    boolean rude,
    TranslationResponse translation,
    List<String> betterExpressions) {}
