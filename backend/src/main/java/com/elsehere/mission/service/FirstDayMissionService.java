package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.favor.repository.FavorRepository;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.entity.Coins;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.Quest;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
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
  private final NpcRepository npcs;
  private final FavorRepository favors;

  @Transactional(readOnly = true)
  public FirstDayMissionResponse status(Long userId) {
    return response(userId, repository.findById(userId).orElse(null));
  }

  public FirstDayMissionResponse start(Long userId) {
    // 사용자 행을 잠가 첫 시작 요청 두 개가 같은 PK를 만들지 않게 한다.
    users.findForUpdate(userId).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    FirstDayMission mission =
        repository.findById(userId).orElseGet(() -> repository.save(FirstDayMission.start(userId)));
    return response(userId, mission);
  }

  public FirstDayMissionResponse visit(Long userId, String scene) {
    if (!List.of("HOME", "TOWN", "CAFE", "HOTEL", "SHOP").contains(scene)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    Optional<FirstDayMission> mission = repository.findForUpdate(userId);
    mission.ifPresent(value -> value.visit(scene));
    return response(userId, mission.orElse(null));
  }

  public FirstDayMissionResponse rest(Long userId) {
    FirstDayMission mission =
        repository
            .findForUpdate(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
    if (mission.getStage() == 2) {
      return response(userId, mission); // 성공한 휴식의 재전송
    }
    if (!"CAFE".equals(mission.getScene())
        || !mission.canRest()
        || conversations
            .findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE)
            .isPresent()) {
      throw new BusinessException(ErrorCode.MISSION_NOT_READY);
    }
    mission.restAtCafe();
    return response(userId, mission);
  }

  @Transactional(readOnly = true)
  public Optional<QuestTalk> context(Long userId, String npcCode) {
    return repository.findById(userId).map(mission -> talk(userId, npcCode, mission));
  }

  // 지금 단계의 대상 NPC면 판정할 목표를 주고, 다른 NPC나 미션을 다 마친 뒤에는 안내만 준다.
  private QuestTalk talk(Long userId, String npcCode, FirstDayMission mission) {
    if (mission.isCompleted()) {
      return new QuestTalk.Guide(
          7,
          "The first-day journey is complete. The learner is checked in, has their suitcase, small blue bag and umbrella. Do not restart the lost-bag incident.");
    }
    FirstDayStage stage = mission.current();
    if (!stage.npc().equals(npcCode)) {
      return new QuestTalk.Guide(
          mission.getStage(),
          QuestPrompts.sendToOwner(place(stage.scene()), ownerName(stage.npc()))
              + " Stay in your own role and help with directions if asked. Do not offer or sell the destination NPC's goods or services, perform their mission actions, claim items found there, or restart completed incidents.");
    }
    String stageInstruction = stage.instruction() + reservationName(userId, stage);
    List<String> remaining = new ArrayList<>();
    for (int index = 0; index < stage.goals().size(); index++) {
      if (!mission.hasGoal(index)) {
        remaining.add(stage.goals().get(index).id());
      }
    }
    WorkerState state =
        new WorkerState(
            "first_day_" + mission.getStage(),
            stageInstruction + QuestPrompts.QUEST_EVIDENCE,
            stage.goals().stream()
                .map(goal -> new WorkerState.Intent(goal.id(), goal.description()))
                .toList(),
            true);
    String instruction;
    if (stage == FirstDayStage.CAFE_BREAK && remaining.isEmpty()) {
      instruction =
          "The learner has already ordered, chosen drink options and said they will stay. Acknowledge the order briefly. Do not offer another drink or repeat order questions. The learner has not rested yet; do not claim that they have.";
    } else {
      instruction =
          stageInstruction
              + " Objectives pending before this turn: "
              + String.join(",", remaining)
              + "."
              + QuestPrompts.LEARNER_LEADS;
    }
    return new QuestTalk.Goals(mission.getStage(), state, instruction, List.copyOf(remaining));
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

  // 예약자 이름을 확인하는 호텔 목표. 아무 이름이나 대도 예약을 확인해 주지 않게 닉네임과 대조한다.
  private static final Map<FirstDayStage, String> RESERVATION_GOALS =
      Map.of(
          FirstDayStage.EARLY_ARRIVAL, "confirm_reservation",
          FirstDayStage.CHECK_IN, "show_reservation");

  /** 예약자 이름 대조 지시문. 손님 계정의 닉네임(guest-000123)은 사람 이름이 아니라서 대조하지 않는다. */
  private String reservationName(Long userId, FirstDayStage stage) {
    String goal = RESERVATION_GOALS.get(stage);
    if (goal == null) {
      return "";
    }
    return users
        .findById(userId)
        .filter(user -> user.getProvider() != AuthProvider.GUEST)
        .map(
            user ->
                " The reservation is under the name \""
                    + user.getNickname()
                    + "\". Count "
                    + goal
                    + " only when the learner gives this name; the same name in English letters or only the family or given name also counts. A different name never counts for "
                    + goal
                    + ", even in the same line as another request: say you cannot find a reservation under that name and ask them to check it.")
        .orElse("");
  }

  // 시작 전과 여정 완료 뒤의 마을 날씨.
  private static final String CLEAR_WEATHER = "CLEAR";

  /** 퀘스트 카드에 보일 예약자 이름. 손님 계정의 닉네임은 사람 이름이 아니라 대조하지 않으므로 아무 이름이나 되게 '내 이름'이라 쓴다. */
  private String reservationHolder(Long userId) {
    return users
        .findById(userId)
        .filter(user -> user.getProvider() != AuthProvider.GUEST)
        .map(user -> user.getNickname())
        .orElse("내 이름");
  }

  private FirstDayMissionResponse response(Long userId, FirstDayMission mission) {
    String holder = reservationHolder(userId);
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
                            definition.id(),
                            definition.title(),
                            done,
                            definition.hint().replace("{name}", holder)));
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
            ? "마을에서 일어난 작은 소동을 해결해 볼까요?"
            : complete
                ? "체크인 완료! 오늘의 작은 소동을 모두 해결했어요."
                : !scene.equals(mission.current().scene())
                    // 다른 장소에 있으면 갈 곳에서 누구에게 무엇을 할지 알린다.
                    ? at(mission.current().scene()) + " " + directionWithNpc(mission.current())
                    : mission.current().situation();
    String direction =
        !started
            ? "퀘스트 카드를 눌러 시작하세요."
            : complete
                ? "다른 NPC와 자유롭게 대화해 보세요."
                : !scene.equals(mission.current().scene())
                    ? travelDirection(mission.current())
                    : directionWithNpc(mission.current());
    return new FirstDayMissionResponse(
        started,
        current,
        Quest.CHECK_IN.name(),
        complete,
        scene,
        started && "CAFE".equals(scene) && mission.canRest(),
        started && current >= 4 && current <= 6 && "TOWN".equals(scene),
        !started || complete ? CLEAR_WEATHER : mission.current().weather(),
        // 코인은 진행에서 계산한다. 산 만큼 줄고 끝내면 보상이 더해지며, 마친 돌발 부탁의 보상도 더한다.
        Coins.balance(mission, null) + Math.toIntExact(favors.sumDoneRewardCoins(userId)),
        Coins.lastPurchase(mission, null),
        situation,
        direction,
        steps);
  }

  private String directionWithNpc(FirstDayStage stage) {
    String direction = stage.direction();
    String name = ownerName(stage.npc());
    if (name == null || name.isBlank()) {
      return direction;
    }
    if (stage == FirstDayStage.RAINY_STREET) {
      return direction.replace("행인 Ben에게", "행인 " + name + "에게");
    }
    return direction + " (대화 상대: " + name + ")";
  }

  private static String destination(String scene) {
    return switch (scene) {
      case "HOTEL" -> "호텔로";
      case "CAFE" -> "카페로";
      case "SHOP" -> "DRUGSTORE 드럭스토어로";
      default -> "마을 거리로";
    };
  }

  private String travelDirection(FirstDayStage stage) {
    String direction = destination(stage.scene()) + " 이동하세요.";
    String name = ownerName(stage.npc());
    return name == null || name.isBlank() ? direction : direction + " (대화 상대: " + name + ")";
  }

  private String ownerName(String npcCode) {
    return npcs.findByCode(npcCode).map(Npc::getName).orElse(null);
  }

  private static String at(String scene) {
    return switch (scene) {
      case "HOTEL" -> "호텔에서";
      case "CAFE" -> "카페에서";
      case "SHOP" -> "DRUGSTORE 드럭스토어에서";
      default -> "마을 거리에서";
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
}
