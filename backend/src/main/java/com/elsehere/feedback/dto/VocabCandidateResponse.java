package com.elsehere.feedback.dto;

/**
 * 피드백에서 단어장에 저장할 수 있는 단어(VOCAB-02 FEEDBACK). 단어 하나만 저장하므로 숙어·문장 후보는 담지 않는다.
 *
 * @param exampleEn 학습용 예문. 단어장에는 저장한 문장으로 보인다. 사용자가 한 말이 아니다
 */
public record VocabCandidateResponse(
    String expression, String meaningKo, String exampleEn, String exampleKo) {}
