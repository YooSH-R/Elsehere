package com.elsehere.vocab.dto;

import java.util.List;

/**
 * VOCAB-01 단어장 목록. 최근 저장순이다.
 *
 * @param counts 현재 조건(외운 단어 포함 여부)을 적용한 개수
 * @param nextCursor 다음 페이지를 요청할 때 보내는 값. 마지막 페이지면 null이다.
 */
public record VocabListResponse(Counts counts, List<VocabItemResponse> items, String nextCursor) {

  public record Counts(long all) {}
}
