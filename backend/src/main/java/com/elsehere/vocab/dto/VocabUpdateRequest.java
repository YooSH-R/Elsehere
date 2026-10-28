package com.elsehere.vocab.dto;

import jakarta.validation.constraints.Size;

/**
 * VOCAB-03 메모·외웠어요 변경 요청. 보낸 필드만 바꾸며, 보내지 않은 필드는 null이다.
 *
 * @param memo 사용자 메모. 최대 {@value #MEMO_MAX_LENGTH}자다.
 * @param memorized true면 외운 단어로 표시하고, false면 표시를 지운다
 */
public record VocabUpdateRequest(@Size(max = MEMO_MAX_LENGTH) String memo, Boolean memorized) {

  public static final int MEMO_MAX_LENGTH = 200;
}
