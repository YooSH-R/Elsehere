package com.elsehere.ai.client;

import java.util.List;

/**
 * 무례 판정(/v1/guard) 결과. 판정하지 못해도 오류가 아니라 판정값 null과 이유 코드로 온다.
 *
 * @param guard 판정. 행동 묘사만 있어 판정하지 않았으면 null
 */
public record GuardResult(Guard guard, List<WorkerUsage> usage) {

  /**
   * @param abusive 무례면 true, 아니면 false, 판정하지 못했으면 null
   * @param errorCode 판정하지 못한 이유(empty·unparsed·timeout·provider_unavailable·provider_limit)
   * @param affinityDelta 무례일 때 NPC 성격을 반영한 호감도 변화량(음수). 워커가 보내지 않으면 null이고 백엔드 기본값을 쓴다
   */
  public record Guard(Boolean abusive, String errorCode, Integer affinityDelta) {

    public Guard(Boolean abusive, String errorCode) {
      this(abusive, errorCode, null);
    }
  }

  public Boolean abusive() {
    return guard == null ? null : guard.abusive();
  }

  public String errorCode() {
    return guard == null ? null : guard.errorCode();
  }

  public Integer affinityDelta() {
    return guard == null ? null : guard.affinityDelta();
  }
}
