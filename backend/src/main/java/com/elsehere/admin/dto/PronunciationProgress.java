package com.elsehere.admin.dto;

/**
 * 사물 단어 발음 진행 상황.
 *
 * @param total 사물 단어(공용 단어 키가 있는 어휘) 수
 * @param ready 그중 발음 파일이 있는 단어 수
 */
public record PronunciationProgress(int total, int ready) {}
