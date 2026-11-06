package com.elsehere.ai.client;

/**
 * 워커 요청의 NPC. 워커는 상태를 저장하지 않으므로 매 요청에 담아 보낸다.
 *
 * @param id NPC 식별자. npcs.code를 쓴다
 * @param ttsSpeed NPC의 타고난 말 속도. NPC 대답은 워커가 난이도 배율을 곱한다
 * @param profile 인격 카드(npcProfile). 칸 이름(camelCase)을 바꾸지 않고 그대로 보낸다. 없으면 null이고, 워커는 role 한 줄로 대답한다
 */
public record WorkerNpc(
    String id,
    String name,
    String role,
    String facts,
    String scene,
    String ttsVoice,
    double ttsSpeed,
    Object profile) {

  public WorkerNpc(
      String id,
      String name,
      String role,
      String facts,
      String scene,
      String ttsVoice,
      double ttsSpeed) {
    this(id, name, role, facts, scene, ttsVoice, ttsSpeed, null);
  }

  /** 장소·상황(scene)만 바꾼 NPC. 워커는 대답·판정·힌트·추천 단어를 이 상황으로 만든다. */
  public WorkerNpc withScene(String scene) {
    return new WorkerNpc(id, name, role, facts, scene, ttsVoice, ttsSpeed, profile);
  }
}
