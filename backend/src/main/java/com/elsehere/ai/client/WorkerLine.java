package com.elsehere.ai.client;

/**
 * 워커 요청의 앞선 대화 한 줄.
 *
 * @param who 학습자 줄은 {@code me}, NPC 줄은 {@code npc}
 * @param english 학습자 줄의 영어 번역. 없으면 null
 * @param rude 학습자 줄이 무례로 판정됐는지. 판정 실패(null)는 false로 보낸다
 */
public record WorkerLine(String who, String text, String english, boolean rude) {

  public static WorkerLine learner(String text, String english) {
    return new WorkerLine("me", text, english, false);
  }

  public static WorkerLine npc(String text) {
    return new WorkerLine("npc", text, null, false);
  }
}
