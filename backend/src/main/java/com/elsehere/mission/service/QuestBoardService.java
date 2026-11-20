package com.elsehere.mission.service;

import com.elsehere.favor.repository.FavorRepository;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.entity.Coins;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.Quest;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 퀘스트 목록. 각 퀘스트의 상태와 들르는 곳을 첫날 미션·마을 이야기 진행에서 계산한다. */
@Service
@Transactional(readOnly = true)
public class QuestBoardService {

  private final FirstDayMissionRepository firstDay;
  private final TownStoryRepository stories;
  private final NpcRepository npcs;
  private final FavorRepository favors;
  private final ShowcaseQuestService showcase;

  public QuestBoardService(
      FirstDayMissionRepository firstDay,
      TownStoryRepository stories,
      NpcRepository npcs,
      FavorRepository favors,
      ShowcaseQuestService showcase) {
    this.firstDay = firstDay;
    this.stories = stories;
    this.npcs = npcs;
    this.favors = favors;
    this.showcase = showcase;
  }

  public QuestBoardResponse board(Long userId) {
    FirstDayMission mission = firstDay.findById(userId).orElse(null);
    TownStory story = stories.findById(userId).orElse(null);
    Map<String, String> names =
        npcs.findAll().stream().collect(Collectors.toMap(Npc::getCode, Npc::getName, (a, b) -> a));
    boolean firstDayDone = mission != null && mission.isCompleted();

    List<QuestBoardResponse.Quest> quests = new ArrayList<>();
    for (Quest quest : Quest.values()) {
      quests.add(
          quest.isTownStory()
              ? chapter(quest, firstDayDone, story, names)
              : firstDayQuest(quest, mission, names));
    }
    // 시연 완료 후에도 다시 시작할 수 있다. 진행 중이던 것을 포함해 끝내지 않은 다른 퀘스트는 계속 잠근다.
    showcase
        .card(userId, names)
        .ifPresent(
            card -> {
              if (showcase.locksOtherQuests(card)) {
                String reason = showcase.lockReason();
                quests.replaceAll(quest -> lockedUnlessDone(quest, reason));
              }
              quests.add(0, card);
            });
    // 시연 중에는 시연 잔액을, 아니면 퀘스트 잔액에 돌발 부탁을 마치고 받은 코인을 더해 보인다.
    int balance =
        showcase
            .activeStatus(userId)
            .map(com.elsehere.mission.dto.FirstDayMissionResponse::balance)
            .orElseGet(
                () ->
                    Coins.balance(mission, story)
                        + Math.toIntExact(favors.sumDoneRewardCoins(userId)));
    return new QuestBoardResponse(balance, quests);
  }

  // 시연이 잠그는 퀘스트. 끝낸 퀘스트는 그대로 두고, 이미 앞 퀘스트 때문에 잠긴 퀘스트는 그 이유를 남긴다.
  private static QuestBoardResponse.Quest lockedUnlessDone(
      QuestBoardResponse.Quest quest, String reason) {
    if ("DONE".equals(quest.status()) || "LOCKED".equals(quest.status())) {
      return quest;
    }
    return new QuestBoardResponse.Quest(
        quest.id(),
        quest.title(),
        quest.description(),
        quest.reward(),
        "LOCKED",
        quest.stops(),
        reason);
  }

  private static QuestBoardResponse.Quest firstDayQuest(
      Quest quest, FirstDayMission mission, Map<String, String> names) {
    boolean done = mission != null && mission.isCompleted();
    int current = mission == null ? -1 : mission.getStage();
    List<QuestBoardResponse.Stop> stops = new ArrayList<>();
    for (FirstDayStage stage : FirstDayStage.values()) {
      int index = stage.ordinal();
      stops.add(
          new QuestBoardResponse.Stop(
              FIRST_DAY_PLACES.getOrDefault(stage.scene(), "거리"),
              names.get(stage.npc()),
              stage.scene(),
              stage.npc(),
              stage.goals().stream()
                  .map(FirstDayStage.Goal::title)
                  .collect(Collectors.joining(" · ")),
              index < current,
              !done && index == current));
    }
    String status = done ? "DONE" : mission != null ? "IN_PROGRESS" : "AVAILABLE";
    return new QuestBoardResponse.Quest(
        quest.name(), quest.title(), quest.description(), quest.reward(), status, stops);
  }

  private static QuestBoardResponse.Quest chapter(
      Quest quest, boolean firstDayDone, TownStory story, Map<String, String> names) {
    int storyStage = story == null ? 0 : story.getStage();
    boolean waiting = story == null || story.isPaused();
    List<QuestBoardResponse.Stop> stops = new ArrayList<>();
    for (int index = quest.firstStoryStage(); index < quest.endStoryStage(); index++) {
      TownStoryStage stage = TownStoryStage.values()[index];
      stops.add(
          new QuestBoardResponse.Stop(
              TOWN_STORY_PLACES.get(stage.npcCode()),
              names.get(stage.npcCode()),
              "TOWN",
              stage.npcCode(),
              stage.goalIds().stream()
                  .map(TownStoryStage::goalTitle)
                  .collect(Collectors.joining(" · ")),
              story != null && index < storyStage,
              !waiting && index == storyStage));
    }
    String status;
    if (!firstDayDone) {
      status = "LOCKED";
    } else if (storyStage >= quest.endStoryStage()) {
      status = "DONE";
    } else if (storyStage >= quest.firstStoryStage()) {
      // 시작하지 않았거나 앞 퀘스트를 끝내고 멈춰 있으면 목록에서 시작할 수 있다.
      status = waiting ? "AVAILABLE" : "IN_PROGRESS";
    } else {
      status = "LOCKED";
    }
    return new QuestBoardResponse.Quest(
        quest.name(),
        quest.title(),
        quest.description(),
        quest.reward(),
        status,
        stops,
        "LOCKED".equals(status) ? previousQuestFirst(quest) : null);
  }

  // 잠긴 이야기 퀘스트는 바로 앞 퀘스트를 마치면 열린다. 어느 퀘스트인지 이름으로 알린다.
  private static String previousQuestFirst(Quest quest) {
    Quest previous = Quest.values()[quest.ordinal() - 1];
    return "'" + previous.title() + "' 퀘스트를 마치면 열려요.";
  }

  private static final Map<String, String> FIRST_DAY_PLACES =
      Map.of("HOTEL", "호텔", "CAFE", "카페", "SHOP", "드럭스토어", "TOWN", "거리");

  private static final Map<String, String> TOWN_STORY_PLACES =
      Map.of(
          "DINER_STAFF", "햄버거 가게",
          "LAUNDROMAT_STAFF", "빨래방",
          "BANK_STAFF", "은행",
          "BARBER_STAFF", "이발소",
          "FLOWER_SHOP_STAFF", "꽃집",
          "BAKERY_STAFF", "빵집",
          "HOME_STORE_STAFF", "생활용품점",
          "ARCADE_STAFF", "오락실",
          "POST_OFFICE_STAFF", "우체국");
}
