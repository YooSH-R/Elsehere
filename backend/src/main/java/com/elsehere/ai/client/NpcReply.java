package com.elsehere.ai.client;

import java.util.List;

/**
 * NPC 대답 스트림(/v1/npc/reply)을 끝까지 받은 결과.
 *
 * @param text 대답 전문. 실패했으면 null
 * @param audioFullId 턴 전체 음성 ID. 합성에 실패했으면 null이며, 워커 메모리에 10분만 남는다
 * @param errorCode 대답 생성이 실패했을 때 워커 오류 코드(provider_unavailable, provider_limit, timeout)
 * @param usage 실패했어도 그때까지 쓴 사용량
 */
public record NpcReply(String text, String audioFullId, String errorCode, List<WorkerUsage> usage) {

  public boolean succeeded() {
    return errorCode == null && text != null;
  }
}
