package com.elsehere.ai.client;

import java.util.List;

/**
 * 워커 요청의 앞선 대화 한 줄.
 *
 * @param who 학습자 줄은 {@code me}, NPC 줄은 {@code npc}
 * @param english 학습자 줄의 생성된 영어 번역·교정문. 원문은 text이며, 없으면 null
 * @param rude 학습자 줄이 무례로 판정됐는지. 판정 실패(null)는 false로 보낸다
 * @param events 학습자 줄에서 의도 판정이 고른 대화 사건. 없으면 빈 목록(워커가 null을 받지 않음)
 */
public record WorkerLine(
    String who, String text, String english, boolean rude, List<String> events) {

  public WorkerLine(String who, String text, String english, boolean rude) {
    this(who, text, english, rude, List.of());
  }

  public static WorkerLine learner(String text, String english, boolean rude) {
    return new WorkerLine("me", text, english, rude);
  }

  public static WorkerLine learner(String text, String english, boolean rude, List<String> events) {
    return new WorkerLine("me", text, english, rude, events == null ? List.of() : events);
  }

  public static WorkerLine npc(String text) {
    return new WorkerLine("npc", text, null, false);
  }
}
