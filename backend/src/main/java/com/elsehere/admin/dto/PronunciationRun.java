package com.elsehere.admin.dto;

import java.util.List;

/**
 * 발음 만들기 한 번의 결과.
 *
 * @param created 새로 만든 발음 수
 * @param failedWordKeys 합성·저장에 실패한 공용 단어 키. 다시 실행하면 이 단어만 다시 만든다
 */
public record PronunciationRun(int created, List<String> failedWordKeys) {}
