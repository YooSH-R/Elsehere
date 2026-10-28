package com.elsehere.ai.client;

import java.util.List;

/**
 * 기억 카드 갱신(/v1/memory) 결과.
 *
 * @param cardJson 새 카드 전체 JSON. 이전 카드를 이것으로 통째로 바꾼다
 */
public record MemoryCardResult(String cardJson, List<WorkerUsage> usage) {}
