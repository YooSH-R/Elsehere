package com.elsehere.ai.client;

import java.util.List;

/**
 * NPC 대사 뜻 보기(/v1/meaning) 결과.
 *
 * @param meaningKo 말투를 살린 한국어 뜻
 * @param glossary 대사에 그대로 있는 내용어·숙어의 풀이(최대 10개). 그런 말이 없으면 빈 목록
 */
public record MeaningResult(String meaningKo, List<Glossary> glossary, List<WorkerUsage> usage) {

  public record Glossary(String text, String meaningKo) {}
}
