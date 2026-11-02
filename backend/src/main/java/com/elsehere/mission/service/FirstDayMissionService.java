package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.user.repository.UserRepository;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional
public class FirstDayMissionService {
  private final FirstDayMissionRepository repository;
  private final ConversationRepository conversations;
  private final UserRepository users;

  @Transactional(readOnly = true)
  public FirstDayMissionResponse status(Long userId) {
    return response(repository.findById(userId).orElse(null));
  }

  public FirstDayMissionResponse start(Long userId) {
    // 사용자 행을 잠가 첫 시작 요청 두 개가 같은 PK를 만들지 않게 한다.
    users.findForUpdate(userId).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    FirstDayMission mission =
        repository.findById(userId).orElseGet(() -> repository.save(FirstDayMission.start(userId)));
    return response(mission);
  }

  public FirstDayMissionResponse visit(Long userId, String scene) {
    if (!List.of("HOME", "TOWN", "CAFE", "HOTEL", "SHOP").contains(scene)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    Optional<FirstDayMission> mission = repository.findForUpdate(userId);
    mission.ifPresent(value -> value.visit(scene));
    return response(mission.orElse(null));
  }

  public FirstDayMissionResponse rest(Long userId) {
    FirstDayMission mission =
        repository
            .findForUpdate(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
    if (mission.getStage() == 2) {
      return response(mission); // 성공한 휴식의 재전송
    }
    if (!"CAFE".equals(mission.getScene())
        || !mission.canRest()
        || conversations
            .findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE)
            .isPresent()) {
      throw new BusinessException(ErrorCode.MISSION_NOT_READY);
    }
    mission.restAtCafe();
    return response(mission);
  }

  @Transactional(readOnly = true)
  public Optional<Context> context(Long userId, String npcCode) {
    return repository
        .findById(userId)
        .map(
            mission -> {
              if (mission.isCompleted()) {
                return new Context(
                    7,
                    null,
                    "The first-day journey is complete. The learner is checked in, has their suitcase, small blue bag and umbrella. Do not restart the lost-bag incident.",
                    List.of());
              }
              FirstDayStage stage = mission.current();
              boolean matches = stage.npc().equals(npcCode);
              List<String> remaining = new ArrayList<>();
              for (int index = 0; index < stage.goals().size(); index++) {
                if (!mission.hasGoal(index)) {
                  remaining.add(stage.goals().get(index).id());
                }
              }
              WorkerState state =
                  matches
                      ? new WorkerState(
                          "first_day_" + mission.getStage(),
                          stage.instruction()
                              + " Recognize only the learner's current utterance in context. Do not credit NPC lines, quoted examples, negatives, wishes without action or future hypothetical actions. One utterance may match several intents.",
                          stage.goals().stream()
                              .map(goal -> new WorkerState.Intent(goal.id(), goal.description()))
                              .toList())
                      : null;
              String instruction;
              if (!matches) {
                instruction =
                    "The learner's next stop is "
                        + place(stage.scene())
                        + ". Stay in your own role and help with directions if asked. Do not offer or sell the destination NPC's goods or services, perform their mission actions, claim items found there, or restart completed incidents.";
              } else if (stage == FirstDayStage.CAFE_BREAK && remaining.isEmpty()) {
                instruction =
                    "The learner has already ordered, chosen drink options and said they will stay. Acknowledge the order briefly. Do not offer another drink or repeat order questions. The learner has not rested yet; do not claim that they have.";
              } else {
                instruction =
                    stage.instruction()
                        + " Objectives pending before this turn: "
                        + String.join(",", remaining);
              }
              return new Context(
                  mission.getStage(),
                  state,
                  instruction,
                  matches ? List.copyOf(remaining) : List.of());
            });
  }

  /** 성공한 턴 저장과 같은 트랜잭션에서만 호출한다. 판정은 허용된 의도의 제안이다. */
  public void accept(Long userId, int expectedStage, String npcCode, JudgeResult.Intent intent) {
    if (intent == null || intent.error() != null) {
      return;
    }
    repository
        .findForUpdate(userId)
        .ifPresent(
            mission ->
                mission.accept(
                    expectedStage, npcCode, intent.ids() == null ? List.of() : intent.ids()));
  }

  // 시작 전과 여정 완료 뒤의 마을 날씨.
  private static final String CLEAR_WEATHER = "CLEAR";

  private static FirstDayMissionResponse response(FirstDayMission mission) {
    boolean started = mission != null;
    int current = started ? mission.getStage() : 0;
    List<FirstDayMissionResponse.Step> steps =
        Arrays.stream(FirstDayStage.values())
            .map(
                stage -> {
                  int index = stage.ordinal();
                  List<FirstDayMissionResponse.Goal> goals = new ArrayList<>();
                  for (int goal = 0; goal < stage.goals().size(); goal++) {
                    FirstDayStage.Goal definition = stage.goals().get(goal);
                    boolean done =
                        started && (index < current || (index == current && mission.hasGoal(goal)));
                    goals.add(
                        new FirstDayMissionResponse.Goal(
                            definition.id(), definition.title(), done));
                  }
                  String status =
                      !started || index > current
                          ? "UPCOMING"
                          : index < current ? "DONE" : "CURRENT";
                  return new FirstDayMissionResponse.Step(
                      index, stage.title(), stage.scene(), stage.npc(), status, goals);
                })
            .toList();
    String scene = started ? mission.getScene() : "HOME";
    boolean complete = started && mission.isCompleted();
    String situation =
        !started
            ? "여행 첫날, 작은 소동을 해결해 볼까요?"
            : complete
                ? "체크인 완료! 오늘의 작은 소동을 모두 해결했어요."
                : !scene.equals(mission.current().scene())
                    ? destination(mission.current().scene()) + " 이동해 다음 이야기를 이어 가세요."
                    : mission.current().situation();
    String direction =
        !started
            ? "왼쪽 위 지도 버튼에서 첫날 여정을 시작하세요."
            : complete
                ? "다른 NPC와 자유롭게 대화해 보세요."
                : !scene.equals(mission.current().scene())
                    ? destination(mission.current().scene()) + " 이동하세요."
                    : mission.current().direction();
    return new FirstDayMissionResponse(
        started,
        current,
        complete,
        scene,
        started && "CAFE".equals(scene) && mission.canRest(),
        started && current >= 4 && current <= 6 && "TOWN".equals(scene),
        !started || complete ? CLEAR_WEATHER : mission.current().weather(),
        started ? mission.getBalance() : 20,
        situation,
        direction,
        steps);
  }

  private static String destination(String scene) {
    return switch (scene) {
      case "HOTEL" -> "호텔로";
      case "CAFE" -> "카페로";
      case "SHOP" -> "DRUGSTORE 드럭스토어로";
      default -> "마을 거리로";
    };
  }

  private static String place(String scene) {
    return switch (scene) {
      case "HOTEL" -> "the hotel";
      case "CAFE" -> "Corner Cafe";
      case "SHOP" -> "the DRUGSTORE";
      case "TOWN" -> "the main street";
      default ->
          throw new IllegalArgumentException("Unsupported first-day mission scene: " + scene);
    };
  }

  public record Context(
      int stage, WorkerState state, String instruction, List<String> remainingGoals) {
    public String replyInstruction(JudgeResult.Intent intent) {
      if (state == null || intent == null || intent.error() != null || intent.ids() == null) {
        return instruction;
      }
      List<String> recognized =
          intent.ids().stream()
              .filter(id -> state.intents().stream().anyMatch(allowed -> allowed.id().equals(id)))
              .toList();
      List<String> pending =
          remainingGoals.stream().filter(id -> !recognized.contains(id)).toList();
      String nextQuestion =
          pending.isEmpty()
              ? " No objectives remain for this stage. Acknowledge what was completed without asking another mission question or assuming later stages are complete."
              : " Remaining objectives after this turn: "
                  + String.join(",", pending)
                  + ". Ask about one of these objectives. Do not repeat already answered questions.";
      return instruction
          + " This turn recognized: "
          + String.join(",", recognized)
          + ". Acknowledge these requests."
          + nextQuestion;
    }
  }
}
