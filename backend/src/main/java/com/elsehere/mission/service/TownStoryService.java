package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.entity.Coins;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.Quest;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Transactional
public class TownStoryService {
  private final TownStoryRepository stories;
  private final FirstDayMissionRepository firstDay;
  private final NpcRepository npcs;

  public TownStoryService(
      TownStoryRepository stories, FirstDayMissionRepository firstDay, NpcRepository npcs) {
    this.stories = stories;
    this.firstDay = firstDay;
    this.npcs = npcs;
  }

  public TownStory start(Long userId) {
    if (firstDay.findById(userId).filter(mission -> mission.isCompleted()).isEmpty()) {
      throw new BusinessException(ErrorCode.MISSION_NOT_READY);
    }
    return stories
        .findById(userId)
        .map(
            story -> {
              // 퀘스트 하나를 끝내고 멈춰 있으면 다음 퀘스트를 시작한다.
              story.resume();
              return story;
            })
        .orElseGet(
            () -> {
              firstDay
                  .findForUpdate(userId)
                  .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
              return stories
                  .findById(userId)
                  .orElseGet(() -> stories.save(TownStory.start(userId)));
            });
  }

  public void accept(Long userId, int stage, String npcCode, JudgeResult.Intent intent) {
    if (intent == null || intent.error() != null || intent.ids() == null) {
      return;
    }
    stories.findForUpdate(userId).ifPresent(story -> story.accept(stage, npcCode, intent.ids()));
  }

  public Optional<FirstDayMissionService.Context> context(Long userId, String npcCode) {
    return stories
        .findById(userId)
        .filter(story -> !story.isCompleted() && !story.isPaused())
        .map(
            story -> {
              TownStoryStage current = story.current();
              if (!current.npcCode().equals(npcCode)) {
                String owner = npcs.findByCode(current.npcCode()).map(Npc::getName).orElse(null);
                return new FirstDayMissionService.Context(
                    story.getStage(),
                    null,
                    FirstDayMissionService.sendToOwner(shop(current.npcCode()), owner)
                        + " Stay in your own role and help the visitor if asked.",
                    List.of());
              }
              List<String> remaining = new ArrayList<>();
              for (int index = 0; index < current.goalIds().size(); index++) {
                if (!story.hasGoal(index)) {
                  remaining.add(current.goalIds().get(index));
                }
              }
              WorkerState state =
                  new WorkerState(
                      "town_story_" + story.getStage(),
                      current.instruction()
                          + " Combine explicit facts across learner turns; the current utterance must contribute. Never credit NPC lines, quoted examples, negatives or hypotheticals.",
                      current.goalIds().stream()
                          .map(id -> new WorkerState.Intent(id, TownStoryStage.goalDescription(id)))
                          .toList(),
                      true);
              return new FirstDayMissionService.Context(
                  story.getStage(),
                  state,
                  current.instruction()
                      + " Objectives pending before this turn: "
                      + String.join(",", remaining)
                      + "."
                      + FirstDayMissionService.LEARNER_LEADS,
                  remaining);
            });
  }

  /**
   * 마을 이야기 상태. 지금 하는 퀘스트(하는 중이 아니면 다음에 시작할 퀘스트)의 단계만 보인다. 단계 번호(stage, index)는 그 퀘스트 안의 순서다. 퀘스트
   * 하나를 끝내고 멈춰 있으면 시작하지 않은 것으로 보인다.
   */
  public FirstDayMissionResponse status(Long userId) {
    TownStory story = stories.findById(userId).orElse(null);
    boolean started = story != null && !story.isPaused();
    int current = story == null ? 0 : story.getStage();
    Quest quest = Quest.townQuestAt(current);
    int first = quest.firstStoryStage();
    List<FirstDayMissionResponse.Step> steps = new ArrayList<>();
    for (int index = first; index < quest.endStoryStage(); index++) {
      TownStoryStage stage = TownStoryStage.values()[index];
      List<FirstDayMissionResponse.Goal> goals = new ArrayList<>();
      for (int goal = 0; goal < stage.goalIds().size(); goal++) {
        String id = stage.goalIds().get(goal);
        boolean done = started && (index < current || (index == current && story.hasGoal(goal)));
        goals.add(new FirstDayMissionResponse.Goal(id, TownStoryStage.goalTitle(id), done));
      }
      String stepStatus =
          !started || index > current ? "UPCOMING" : index < current ? "DONE" : "CURRENT";
      steps.add(
          new FirstDayMissionResponse.Step(
              index - first, stage.title(), "TOWN", stage.npcCode(), stepStatus, goals));
    }
    boolean completed = started && story.isCompleted();
    FirstDayMission firstDayMission = firstDay.findById(userId).orElse(null);
    String scene =
        firstDayMission == null || firstDayMission.getScene() == null
            ? "TOWN"
            : firstDayMission.getScene();
    String situation =
        story == null
            ? "마을에서 새로운 하루를 시작해 볼까요?"
            : !started
                ? "새 퀘스트가 열렸어요. 퀘스트 목록에서 시작해 보세요."
                : completed
                    ? "마을에서의 하루를 마쳤어요."
                    : !"TOWN".equals(scene)
                        ? "마을로 나가 " + talkTo(story.current().npcCode())
                        : story.current().situation();
    return new FirstDayMissionResponse(
        started,
        started ? current - first : 0,
        quest.name(),
        completed,
        scene,
        false,
        false,
        "CLEAR",
        // 코인은 첫날 퀘스트와 마을 이야기 진행에서 함께 계산한다.
        Coins.balance(firstDayMission, story),
        Coins.lastPurchase(firstDayMission, story),
        situation,
        !started
            ? "퀘스트 목록에서 퀘스트를 시작하세요."
            : completed
                ? "마을의 이웃들과 자유롭게 대화해 보세요."
                : !"TOWN".equals(scene)
                    ? "마을로 나가 다음 목표를 확인하세요."
                    : talkTo(story.current().npcCode()),
        steps);
  }

  /** 퀘스트 카드에 보일 '지금 만날 직원' 안내. 직원 정보를 찾지 못하면 직원이라고만 쓴다. */
  private String talkTo(String npcCode) {
    return npcs.findByCode(npcCode)
        .map(npc -> npc.getRoleLabel() + " " + npc.getName() + "에게 이야기해 보세요.")
        .orElse("직원에게 이야기해 보세요.");
  }

  // 마을 이야기 직원이 있는 가게. NPC 대답은 영어라 영어 이름을 쓴다.
  private static String shop(String npcCode) {
    return switch (npcCode) {
      case "DINER_STAFF" -> "the diner";
      case "LAUNDROMAT_STAFF" -> "the laundromat";
      case "BANK_STAFF" -> "the bank";
      case "BARBER_STAFF" -> "the barbershop";
      case "FLOWER_SHOP_STAFF" -> "the flower shop";
      case "BAKERY_STAFF" -> "the bakery";
      case "HOME_STORE_STAFF" -> "the home goods store";
      case "ARCADE_STAFF" -> "the arcade";
      case "POST_OFFICE_STAFF" -> "the post office";
      default -> "the main street";
    };
  }
}
