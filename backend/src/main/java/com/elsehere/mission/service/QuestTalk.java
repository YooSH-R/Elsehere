package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import java.util.List;
import java.util.Objects;
import java.util.Optional;
import java.util.stream.Collectors;

/**
 * 퀘스트 대화 목표. NPC와 대화할 때 첫날 미션({@link FirstDayMissionService}), 마을 이야기({@link TownStoryService}), 장소
 * 연습({@link PlacePracticeService})이 그 NPC에게 주는 지시와 판정할 목표다.
 *
 * <p>두 종류다.
 *
 * <ul>
 *   <li>{@link Goals}: 이 NPC에게 판정할 목표가 있다(지금 단계의 대상 NPC, 남은 목표가 있는 장소 연습). 의도 판정이 알아본 목표가 대답 지시에
 *       들어간다.
 *   <li>{@link Guide}: 안내만 한다(미션과 상관없는 NPC, 미션을 다 마친 뒤, 목표를 다 이룬 장소 연습). 판정 결과와 관계없이 지시가 같다.
 * </ul>
 */
public sealed interface QuestTalk {

  /** 퀘스트 단계. 장소 연습은 상황 번호다. 진행을 넘길 때 지금 단계인지 확인하는 데 쓴다. */
  int stage();

  /** 판정 전 지시. 첫 인사와 판정하지 못한 턴에 쓴다. */
  String instruction();

  /** 이번 턴의 대답 지시. 판정할 목표가 있으면 판정이 알아본 목표와 남은 목표를 덧붙인다. */
  String replyInstruction(JudgeResult.Intent intent);

  /** 이 NPC에게 남은 목표가 있는지. 안내만 하는 대화나 목표를 다 마친 단계는 없다. */
  boolean hasGoals();

  /** 이 턴에 알아본 목표로 이 NPC에게 남은 목표가 모두 끝나는지. */
  boolean completesWith(JudgeResult.Intent intent);

  /** 판정할 목표가 있는 대화면 그 목표, 안내만 하는 대화면 빈 값. */
  Optional<Goals> goals();

  /**
   * 이 NPC에게 판정할 목표가 있는 대화.
   *
   * @param state 의도 판정에 보내는 상태. 판정할 목표(의도) 목록을 담는다
   * @param remainingGoals 이 턴 전까지 남은 목표. 단계의 목표를 다 마쳤으면 빈 목록이다
   */
  record Goals(int stage, WorkerState state, String instruction, List<String> remainingGoals)
      implements QuestTalk {

    public Goals {
      Objects.requireNonNull(state, "판정할 목표가 있는 대화에는 판정 상태가 있어야 한다");
    }

    @Override
    public String replyInstruction(JudgeResult.Intent intent) {
      if (!judged(intent)) {
        return instruction;
      }
      List<String> recognized = recognized(intent);
      List<String> pending =
          remainingGoals.stream().filter(id -> !recognized.contains(id)).toList();
      String pendingDetails =
          state.intents().stream()
              .filter(goal -> pending.contains(goal.id()))
              .map(goal -> goal.id() + ": " + goal.desc())
              .collect(Collectors.joining("\n"));
      String nextQuestion =
          pending.isEmpty()
              ? " No objectives remain for this stage. Acknowledge what was completed without asking another mission question or assuming later stages are complete."
              : " Remaining objectives after this turn: "
                  + String.join(",", pending)
                  + ". Required details for those objectives:\n"
                  + pendingDetails;
      return instruction
          + " This turn recognized: "
          + String.join(",", recognized)
          + ". Acknowledge these requests."
          + nextQuestion;
    }

    @Override
    public boolean hasGoals() {
      return !remainingGoals.isEmpty();
    }

    @Override
    public boolean completesWith(JudgeResult.Intent intent) {
      return hasGoals() && judged(intent) && recognized(intent).containsAll(remainingGoals);
    }

    @Override
    public Optional<Goals> goals() {
      return Optional.of(this);
    }

    private static boolean judged(JudgeResult.Intent intent) {
      return intent != null && intent.error() == null && intent.ids() != null;
    }

    private List<String> recognized(JudgeResult.Intent intent) {
      return intent.ids().stream()
          .filter(id -> state.intents().stream().anyMatch(allowed -> allowed.id().equals(id)))
          .toList();
    }
  }

  /** 안내만 하는 대화. 판정 결과와 관계없이 같은 지시를 쓴다. */
  record Guide(int stage, String instruction) implements QuestTalk {

    @Override
    public String replyInstruction(JudgeResult.Intent intent) {
      return instruction;
    }

    @Override
    public boolean hasGoals() {
      return false;
    }

    @Override
    public boolean completesWith(JudgeResult.Intent intent) {
      return false;
    }

    @Override
    public Optional<Goals> goals() {
      return Optional.empty();
    }
  }
}
