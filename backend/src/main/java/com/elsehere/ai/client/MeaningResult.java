package com.elsehere.ai.client;

import java.util.List;

/**
 * NPC 대사 뜻 보기(/v1/meaning) 결과.
 *
 * @param meaningKo 말투를 살린 한국어 뜻
 * @param glossary 대사에 그대로 있는 말 중 학습자가 모를 만한 말(최대 3개). 쉬운 대사면 빈 목록
 */
public record MeaningResult(String meaningKo, List<Glossary> glossary, List<WorkerUsage> usage) {

  public record Glossary(String text, String meaningKo) {}
}
