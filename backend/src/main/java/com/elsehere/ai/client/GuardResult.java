package com.elsehere.ai.client;

import java.util.List;

/**
 * 무례 판정(/v1/guard) 결과. 판정하지 못해도 오류가 아니라 판정값 null과 이유 코드로 온다.
 *
 * @param guard 판정. 행동 묘사만 있어 판정하지 않았으면 null
 * @param affinityDelta 무례로 판정했을 때 NPC 성격을 반영해 깎을 호감도(음수). 무례가 아니거나 판정하지 못했으면 null이고, 무례인데 비어 있으면(이
 *     칸을 모르는 이전 워커) 백엔드 기본값을 쓴다
 */
public record GuardResult(Guard guard, Integer affinityDelta, List<WorkerUsage> usage) {

  public GuardResult(Guard guard, List<WorkerUsage> usage) {
    this(guard, null, usage);
  }

  /**
   * @param abusive 무례면 true, 아니면 false, 판정하지 못했으면 null
   * @param errorCode 판정하지 못한 이유(empty·unparsed·timeout·provider_unavailable·provider_limit)
   */
  public record Guard(Boolean abusive, String errorCode) {}

  public Boolean abusive() {
    return guard == null ? null : guard.abusive();
  }

  public String errorCode() {
    return guard == null ? null : guard.errorCode();
  }
}
