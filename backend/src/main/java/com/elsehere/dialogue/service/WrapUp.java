package com.elsehere.dialogue.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.MissionDialogueService;
import java.util.List;
import java.util.Optional;

/**
 * 이 턴으로 NPC가 마무리 대사를 하고 대화를 끝낼지 정한다(2026-11-04 결정). 줄 사건은 의도 판정이 고른다(docs/api/ai-worker.md 2.2절).
 *
 * <ul>
 *   <li>이 NPC에게 남은 미션 목표가 있으면 이 턴으로 목표가 모두 끝날 때 마무리한다. 학습자가 작별(LEAVE)하면 목표가 남아도 마무리하며, 다시 말을 걸어 이어
 *       할 수 있다.
 *   <li>그 밖의 대화는 학습자가 작별하거나 용건을 끝냈다고 하면(TASK_DONE) 마무리한다.
 *   <li>무례로 센 턴은 무례 처리만 하고 마무리하지 않는다.
 * </ul>
 */
final class WrapUp {

  // 마무리 턴의 NPC 지시문. 이번 대답의 지시문(미션 등) 뒤에 붙는다.
  static final String INSTRUCTION =
      "This is your last line in this conversation: reply to what they said, then close it warmly in"
          + " your own way (a goodbye, or that what they ordered will be ready soon). Do not ask another"
          + " question or start a new topic.";

  private static final String LEAVE = "LEAVE";
  private static final String TASK_DONE = "TASK_DONE";

  private WrapUp() {}

  static boolean ends(
      boolean rude, Optional<MissionDialogueService.Context> mission, JudgeResult judge) {
    if (rude) {
      return false;
    }
    List<String> events = judge.events() == null ? List.of() : judge.events();
    if (events.contains(LEAVE)) {
      return true;
    }
    return mission
        .map(MissionDialogueService.Context::detail)
        .filter(FirstDayMissionService.Context::hasGoals)
        .map(detail -> detail.completesWith(judge.intent()))
        .orElse(events.contains(TASK_DONE));
  }

  static String instruction(String instruction) {
    return instruction.isEmpty() ? INSTRUCTION : instruction + " " + INSTRUCTION;
  }
}
