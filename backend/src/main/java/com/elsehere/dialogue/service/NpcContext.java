package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import java.util.Map;

/**
 * 대화에 필요한 NPC 정보. AI 호출 동안 트랜잭션을 열어 두지 않도록 엔티티에서 미리 꺼내 둔다.
 *
 * @param voiceId 음성 합성 목소리
 * @param approachLine 행인 말걸기 대사. 말을 걸지 않는 NPC는 null
 * @param approachAudioKey 말걸기 대사의 미리 만든 음성. 없으면 null
 */
public record NpcContext(
    Long npcId,
    NpcSummaryResponse summary,
    String sceneCode,
    boolean sceneOpen,
    WorkerNpc workerNpc,
    String voiceId,
    String approachLine,
    String approachAudioKey) {

  /** 대화를 시작한 순간 NPC의 생활 욕구를 워커 NPC에 싣는다. 대답 요청의 needs로 가서 바탕 기분이 된다. 없으면 그대로 둔다. */
  NpcContext withNeeds(Map<String, Double> needs) {
    if (needs == null) {
      return this;
    }
    return new NpcContext(
        npcId,
        summary,
        sceneCode,
        sceneOpen,
        workerNpc.withNeeds(needs),
        voiceId,
        approachLine,
        approachAudioKey);
  }

  NpcContext withTravelSituation(String situation) {
    if (situation == null || situation.isBlank()) {
      return this;
    }
    return new NpcContext(
        npcId,
        summary,
        sceneCode,
        sceneOpen,
        workerNpc.withScene(workerNpc.scene() + " " + situation),
        voiceId,
        approachLine,
        approachAudioKey);
  }

  /**
   * 근무 시간이 아닌 NPC. 워커에 보내는 상황(npc.scene)만 응대 상황에서 일상 상황으로 바꾼다.
   *
   * <p>역할·사실·인격 카드는 그대로 보낸다. 사적인 이야기를 얼마나 할지는 워커가 인격 카드와 알아봄·호감으로 정하므로, 이 상황이 친한 사이라고 말하지 않는다.
   */
  NpcContext offDuty() {
    return new NpcContext(
        npcId,
        summary,
        sceneCode,
        sceneOpen,
        workerNpc.withScene(offDutyScene(workerNpc.name())),
        voiceId,
        approachLine,
        approachAudioKey);
  }

  private static String offDutyScene(String name) {
    return name
        + " is off work right now and is spending free time, not working. "
        + name
        + " does not take orders or serve anyone now and just makes casual small talk.";
  }
}
