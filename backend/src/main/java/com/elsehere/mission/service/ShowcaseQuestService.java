package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.user.repository.UserRepository;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional
public class ShowcaseQuestService {
  private final ShowcaseSettings settings;
  private final ShowcaseJourneyRepository journeys;
  private final UserRepository users;
  private final ConversationRepository conversations;
  private final ShowcaseScene scene;

  @Transactional(readOnly = true)
  public Optional<QuestTalk> context(Long userId, String npcCode) {
    return active(userId).map(journey -> talk(journey, npcCode));
  }

  private QuestTalk talk(ShowcaseJourney journey, String npcCode) {
    if (journey.isCompleted()) {
      return new QuestTalk.Guide(
          journey.getStage(),
          "The rainy-evening trip is complete. Stay in your own role and continue naturally without starting check-in, a lost-bag incident or a laundry mission.");
    }
    ShowcaseStage stage = journey.current();
    if (!stage.npcCode().equals(npcCode)) {
      String destination =
          switch (stage) {
            case HOTEL -> "the hotel";
            case TAXI -> "the taxi outside the hotel";
            case ORDER -> "Maple Burger";
          };
      return new QuestTalk.Guide(
          journey.getStage(),
          QuestPrompts.sendToOwner(destination, null)
              + " Stay in your own role. Never offer, sell, lend or claim to have done another NPC's task.");
    }
    // 기사에게 판정 목표를 주지 않아 기존 목적지 슬롯(go_to / destination)을 그대로 사용한다.
    if (stage == ShowcaseStage.TAXI) {
      return new QuestTalk.Guide(journey.getStage(), stage.instruction());
    }
    List<String> pending = new ArrayList<>();
    for (int i = 0; i < stage.goals().size(); i++) {
      if (!journey.hasGoal(i)) {
        pending.add(stage.goals().get(i).id());
      }
    }
    String instruction =
        stage.instruction()
            + QuestPrompts.QUEST_EVIDENCE
            + " Pending learner objectives: "
            + String.join(",", pending)
            + ".";
    WorkerState state =
        new WorkerState(
            "showcase_" + stage.name().toLowerCase(java.util.Locale.ROOT),
            stage.instruction() + QuestPrompts.QUEST_EVIDENCE,
            stage.goals().stream()
                .map(goal -> new WorkerState.Intent(goal.id(), goal.description()))
                .toList(),
            true);
    // 주문 단계는 세부 목표가 넷이라, 하나를 마치면 직원이 다음 목표를 물어 대화를 이끈다. 호텔 단계는 학습자의 부탁을 기다린다.
    boolean leadsToNextGoal = stage == ShowcaseStage.ORDER;
    return new QuestTalk.Goals(
        journey.getStage(), state, instruction, List.copyOf(pending), leadsToNextGoal);
  }

  public void accept(Long userId, int expectedStage, String npcCode, JudgeResult.Intent intent) {
    if (intent == null || intent.error() != null) {
      return;
    }
    journeys
        .findForUpdate(userId)
        .filter(ShowcaseJourney::isActive)
        .ifPresent(
            journey -> {
              if (journey.getStage() != expectedStage) {
                return;
              }
              if (journey.current() == ShowcaseStage.TAXI
                  && intent.ids() != null
                  && intent.ids().contains("go_to")
                  && intent.slots() != null) {
                Object destination = intent.slots().get("destination");
                boolean burger =
                    "Maple Diner".equals(destination) || "Maple Burger".equals(destination);
                journey.chooseTaxiDestination(npcCode, burger ? "DINER" : null);
              } else {
                journey.accept(expectedStage, npcCode, intent.ids());
              }
            });
  }

  @Transactional(readOnly = true)
  public Optional<FirstDayMissionResponse> activeStatus(Long userId) {
    return active(userId).map(this::response);
  }

  @Transactional(readOnly = true)
  public FirstDayMissionResponse status(Long userId) {
    return response(journeys.findById(userId).orElseGet(() -> ShowcaseJourney.notStarted(userId)));
  }

  /**
   * 시연 전용 서버(설정 {@code elsehere.showcase.enabled})인지. 이 서버에서는 모든 계정이 시작 전부터 시연 상태이고 다른 퀘스트는 시연을 끝낼
   * 때까지 잠긴다. 그 밖의 서버(dev·운영)는 퀘스트 목록의 시연 카드를 눌러 시작한 계정만 진행하는 동안 시연 상태다.
   */
  public boolean isShowcaseServer() {
    return settings.enabled();
  }

  /** 퀘스트 목록의 시연 카드가 다른 퀘스트를 잠그는지. 시연 전용 서버는 끝내기 전까지, 그 밖의 서버는 진행하는 동안만 잠근다. */
  public boolean locksOtherQuests(QuestBoardResponse.Quest card) {
    return settings.enabled() ? !"DONE".equals(card.status()) : "IN_PROGRESS".equals(card.status());
  }

  /** 첫날 상태 대신 보일 시연 상태. 시연 전용 서버는 시작 전에도 보여 다른 퀘스트가 진행 중으로 보이지 않게 하고, 그 밖의 서버는 시연을 진행하는 동안만 보인다. */
  @Transactional(readOnly = true)
  public Optional<FirstDayMissionResponse> shownStatus(Long userId) {
    if (!settings.enabled()) {
      return activeStatus(userId);
    }
    return Optional.of(
        response(journeys.findById(userId).orElseGet(() -> ShowcaseJourney.notStarted(userId))));
  }

  /** 장소에 들어왔다. 하는 중이면 장소를 남기고, 시작 전이어도 시연 퀘스트를 돌려준다. 시연이 꺼져 있으면 비어 있다. */
  public Optional<FirstDayMissionResponse> visit(Long userId, String scene) {
    if (!settings.enabled() && active(userId).isEmpty()) {
      // 시연을 끝낸 대화를 닫지 않고 나갔어도 다음에 장소에 들어올 때 계정을 돌려놓는다.
      leaveIfFinished(userId);
      return Optional.empty();
    }
    if (!List.of("HOME", "TOWN", "HOTEL", "CAFE", "SHOP").contains(scene)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    ShowcaseJourney journey =
        journeys.findForUpdate(userId).orElseGet(() -> ShowcaseJourney.notStarted(userId));
    if (journey.isActive()) {
      journey.visit(scene);
    }
    return Optional.of(response(journey));
  }

  public FirstDayMissionResponse start(Long userId) {
    lockUserWithoutConversation(userId);
    ShowcaseJourney journey = journeys.findForUpdate(userId).orElse(null);
    // 끝냈으면(주문 완료) 처음부터 다시 한다.
    boolean finished = journey != null && journey.isCompleted();
    // 처음부터 시작할 때만 대본의 출발 상태(NPC 관계·게임 시각)를 맞춘다. 멈췄다 이어 하면 멈춘 때의 시연 상태를 되살린다.
    boolean fromBeginning =
        journey == null || finished || (journey.getStage() == 0 && !journey.isActive());
    if (journey == null) {
      journey = journeys.save(ShowcaseJourney.start(userId));
    }
    if (finished) {
      journey.restart();
    }
    journey.resume();
    if (fromBeginning) {
      scene.setUp(userId);
    } else {
      scene.resume(userId);
    }
    return response(journey);
  }

  public FirstDayMissionResponse reset(Long userId) {
    lockUserWithoutConversation(userId);
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .orElseGet(() -> journeys.save(ShowcaseJourney.start(userId)));
    journey.restart();
    scene.leave(userId);
    return response(journey);
  }

  public FirstDayMissionResponse stop(Long userId) {
    lockUserWithoutConversation(userId);
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
    journey.stop();
    scene.pause(userId);
    return response(journey);
  }

  /**
   * 공용 서버에서 시연을 끝냈으면 시연이 덮어쓴 NPC 관계와 게임 시각을 시작 전 값으로 돌려놓는다. 시연 전용 서버는 끝낸 뒤에도 시연 맥락을 유지해 돌려놓지 않는다.
   *
   * @return 이번에 돌려놓았는지
   */
  public boolean leaveIfFinished(Long userId) {
    if (settings.enabled()) {
      return false;
    }
    return journeys
        .findById(userId)
        .filter(ShowcaseJourney::isCompleted)
        .map(journey -> scene.leave(userId))
        .orElse(false);
  }

  public FirstDayMissionResponse taxiArrival(Long userId, String destination) {
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .filter(ShowcaseJourney::isActive)
            .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
    journey.arriveByTaxi(destination);
    return response(journey);
  }

  private void lockUserWithoutConversation(Long userId) {
    users.findForUpdate(userId).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (conversations.findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE).isPresent()) {
      throw new BusinessException(ErrorCode.MISSION_NOT_READY);
    }
  }

  private Optional<ShowcaseJourney> active(Long userId) {
    // 시연 전용 서버는 끝낸 뒤에도 시연 맥락을 유지한다(대화 노트·다시 연습으로 이어 감). 그 밖의 서버는 끝내면 일반 진행으로 돌아간다.
    return journeys
        .findById(userId)
        .filter(journey -> journey.isActive() && (settings.enabled() || !journey.isCompleted()));
  }

  @Transactional(readOnly = true)
  public Optional<QuestBoardResponse.Quest> card(Long userId, Map<String, String> names) {
    ShowcaseJourney journey = journeys.findById(userId).orElse(null);
    boolean showProgress = journey != null && !journey.isCompleted();
    List<QuestBoardResponse.Stop> stops = new ArrayList<>();
    for (ShowcaseStage stage : ShowcaseStage.values()) {
      int index = stage.ordinal();
      String place =
          switch (stage) {
            case HOTEL -> "호텔";
            case TAXI -> "거리";
            case ORDER -> "햄버거 가게";
          };
      stops.add(
          new QuestBoardResponse.Stop(
              place,
              names.get(stage.npcCode()),
              stage.scene(),
              stage.npcCode(),
              stage.title(),
              showProgress && index < journey.getStage(),
              showProgress && journey.isActive() && index == journey.getStage()));
    }
    String status =
        journey == null || journey.isCompleted()
            ? "AVAILABLE"
            : journey.isActive() ? "IN_PROGRESS" : "AVAILABLE";
    return Optional.of(
        new QuestBoardResponse.Quest(
            ShowcaseStage.QUEST_ID,
            "비 오는 저녁, 햄버거 한 끼",
            "[시연] 우산을 빌려 택시로 햄버거 가게에 가서 햄버거를 주문해요.",
            0,
            status,
            stops));
  }

  private FirstDayMissionResponse response(ShowcaseJourney journey) {
    List<FirstDayMissionResponse.Step> steps = new ArrayList<>();
    for (ShowcaseStage stage : ShowcaseStage.values()) {
      int index = stage.ordinal();
      boolean done = index < journey.getStage();
      boolean current = journey.isActive() && index == journey.getStage();
      List<FirstDayMissionResponse.Goal> goals = new ArrayList<>();
      for (int i = 0; i < stage.goals().size(); i++) {
        ShowcaseStage.Goal goal = stage.goals().get(i);
        goals.add(
            new FirstDayMissionResponse.Goal(
                goal.id(), goal.title(), done || (current && journey.hasGoal(i)), goal.hint()));
      }
      steps.add(
          new FirstDayMissionResponse.Step(
              index,
              stage.title(),
              stage.scene(),
              stage.npcCode(),
              done ? "DONE" : current ? "CURRENT" : "PENDING",
              goals));
    }
    String situation =
        journey.isCompleted() ? "대화 노트에서 햄버거 주문 대화를 열고 다시 연습해 보세요." : journey.current().situation();
    return new FirstDayMissionResponse(
        journey.isActive(),
        journey.getStage(),
        ShowcaseStage.QUEST_ID,
        journey.isCompleted(),
        journey.getScene(),
        false,
        "RAIN".equals(journey.weather()),
        journey.weather(),
        journey.balance(),
        journey.isMealPaid() ? "햄버거 세트" : null,
        situation,
        situation,
        steps);
  }
}
