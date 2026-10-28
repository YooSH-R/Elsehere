package com.elsehere.vocab.dto;

import com.elsehere.vocab.entity.UserVocabSourceType;
import jakarta.validation.constraints.NotNull;

/**
 * VOCAB-02 단어 저장 요청. 출처({@code sourceType})마다 쓰는 필드가 다르다.
 *
 * @param requestId 명세 형식대로 받지만 쓰지 않는다. 이미 저장한 단어는 처음 항목을 돌려주므로 다시 보내도 결과가 같다.
 * @param wordKey OBJECT: 사물 카드의 공용 단어 키(vocabularies.word_key)
 * @param turnId NPC_LINE: 단어 풀이를 본 NPC 대사 턴
 * @param conversationId FEEDBACK: 단어장 후보가 나온 대화
 * @param text NPC_LINE·FEEDBACK: 저장할 단어 하나
 */
public record VocabSaveRequest(
    String requestId,
    @NotNull UserVocabSourceType sourceType,
    String wordKey,
    Long turnId,
    Long conversationId,
    String text) {}
