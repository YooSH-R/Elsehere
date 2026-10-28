package com.elsehere.vocab.dto;

import com.fasterxml.jackson.annotation.JsonUnwrapped;

/**
 * VOCAB-02 단어 저장 결과. 단어장 항목(VOCAB-01) 필드에 {@code created}를 더한 형식이다.
 *
 * @param created 새로 저장했으면 true, 이미 저장한 단어면 false
 */
public record VocabSaveResponse(@JsonUnwrapped VocabItemResponse item, boolean created) {}
