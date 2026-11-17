package com.elsehere.mission.service;

import com.elsehere.ai.client.WorkerState;
import com.elsehere.mission.entity.ShowcaseStage;
import java.util.List;
import java.util.Locale;
import java.util.Optional;

/**
 * 시연 상황을 [다시 연습]할 때 판정할 조건. 복습 카드에 저장한 상황 키("showcase_단계")의 목표를 진행 중인 시연과 상관없이 처음부터 판정한다. 상황 설명과
 * 연습할 표현은 복습 지시문이 이미 보내므로, NPC 지시문 제한(3,000자) 안에 들도록 여기서는 남은 조건만 짧게 적는다.
 */
public final class ShowcaseReviewGoals {
  private static final String KEY_PREFIX = "showcase_";
  private static final String ALL_DONE =
      "All practice objectives are complete. Confirm briefly and close politely without starting another request.";

  private ShowcaseReviewGoals() {}

  /** 이 대화에서 아직 말하지 않은 조건을 판정한다. 시연 상황이 아니거나 조건이 없는 단계면 빈 값이다. */
  public static Optional<QuestTalk> talk(String scenarioKey, List<String> completedGoalIds) {
    return stage(scenarioKey).map(stage -> talk(stage, completedGoalIds));
  }

  /** 카드에 보일 조건(이름과 말할 내용). 시연 상황이 아니면 빈 목록이다. */
  public static List<ShowcaseStage.Goal> goals(String scenarioKey) {
    return stage(scenarioKey).map(ShowcaseStage::goals).orElse(List.of());
  }

  private static QuestTalk talk(ShowcaseStage stage, List<String> completedGoalIds) {
    List<ShowcaseStage.Goal> remaining =
        stage.goals().stream().filter(goal -> !completedGoalIds.contains(goal.id())).toList();
    if (remaining.isEmpty()) {
      return new QuestTalk.Guide(stage.ordinal(), ALL_DONE);
    }
    List<String> remainingIds = remaining.stream().map(ShowcaseStage.Goal::id).toList();
    WorkerState state =
        new WorkerState(
            "showcase_review_" + stage.name().toLowerCase(Locale.ROOT),
            stage.instruction() + QuestPrompts.QUEST_EVIDENCE,
            remaining.stream()
                .map(goal -> new WorkerState.Intent(goal.id(), goal.description()))
                .toList(),
            true);
    return new QuestTalk.Goals(
        stage.ordinal(),
        state,
        "Practice objectives pending before this turn: " + String.join(",", remainingIds) + ".",
        remainingIds);
  }

  private static Optional<ShowcaseStage> stage(String scenarioKey) {
    if (scenarioKey == null || !scenarioKey.startsWith(KEY_PREFIX)) {
      return Optional.empty();
    }
    try {
      ShowcaseStage stage = ShowcaseStage.valueOf(scenarioKey.substring(KEY_PREFIX.length()));
      // 택시는 목적지를 기존 목적지 판정으로 알아듣고 조건 목록을 쓰지 않는다.
      return stage == ShowcaseStage.TAXI || stage.goals().isEmpty()
          ? Optional.empty()
          : Optional.of(stage);
    } catch (IllegalArgumentException unknown) {
      return Optional.empty();
    }
  }
}
