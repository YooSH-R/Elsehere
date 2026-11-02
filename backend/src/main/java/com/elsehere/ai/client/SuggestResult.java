package com.elsehere.ai.client;

import java.util.List;

/**
 * 추천 단어(/v1/suggest) 결과.
 *
 * @param words 학습자가 다음 대답에 쓸 만한 단어·짧은 구(최대 3개)
 */
public record SuggestResult(List<String> words, List<WorkerUsage> usage) {}
