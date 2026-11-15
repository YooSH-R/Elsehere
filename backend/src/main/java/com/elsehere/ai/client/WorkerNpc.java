package com.elsehere.ai.client;

import com.fasterxml.jackson.annotation.JsonIgnore;
import java.util.Map;

/**
 * 워커 요청의 NPC. 워커는 상태를 저장하지 않으므로 매 요청에 담아 보낸다.
 *
 * @param id NPC 식별자. npcs.code를 쓴다
 * @param ttsSpeed 호환용 필드. NPC 대화 음성 속도는 1.1배로 고정한다
 * @param profile 인격 카드(npcProfile). 칸 이름(camelCase)을 바꾸지 않고 그대로 보낸다. 없으면 null이고, 워커는 role 한 줄로 대답한다
 * @param needs 대화를 시작한 순간 NPC의 생활 욕구(공통 규칙의 욕구 키, 0~100). npc 안에는 넣지 않고 대답 요청 맨 위의 needs로 보낸다. 생활하지
 *     않는 NPC면 null
 */
public record WorkerNpc(
    String id,
    String name,
    String role,
    String facts,
    String scene,
    String ttsVoice,
    double ttsSpeed,
    Object profile,
    @JsonIgnore Map<String, Double> needs) {

  public WorkerNpc(
      String id,
      String name,
      String role,
      String facts,
      String scene,
      String ttsVoice,
      double ttsSpeed,
      Object profile) {
    this(id, name, role, facts, scene, ttsVoice, ttsSpeed, profile, null);
  }

  public WorkerNpc(
      String id,
      String name,
      String role,
      String facts,
      String scene,
      String ttsVoice,
      double ttsSpeed) {
    this(id, name, role, facts, scene, ttsVoice, ttsSpeed, null, null);
  }

  /** 장소·상황(scene)만 바꾼 NPC. 워커는 대답·판정·힌트·추천 단어를 이 상황으로 만든다. */
  public WorkerNpc withScene(String scene) {
    return new WorkerNpc(id, name, role, facts, scene, ttsVoice, ttsSpeed, profile, needs);
  }

  /** 대화를 시작한 순간의 욕구를 실은 NPC. */
  public WorkerNpc withNeeds(Map<String, Double> needs) {
    return new WorkerNpc(id, name, role, facts, scene, ttsVoice, ttsSpeed, profile, needs);
  }
}
