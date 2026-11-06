package com.elsehere.ai.client;

import java.util.List;

/**
 * 기억 카드 갱신(/v1/memory) 결과.
 *
 * @param cardJson 새 카드 전체 JSON. 이전 카드를 이것으로 통째로 바꾼다
 * @param affinityDelta 이번 대화가 좋았을 때 올릴 호감도. 좋은 대화가 아니면 0이며, 워커가 보내지 않으면(이 칸을 모르는 이전 워커) null
 */
public record MemoryCardResult(String cardJson, Integer affinityDelta, List<WorkerUsage> usage) {

  public MemoryCardResult(String cardJson, List<WorkerUsage> usage) {
    this(cardJson, null, usage);
  }
}
