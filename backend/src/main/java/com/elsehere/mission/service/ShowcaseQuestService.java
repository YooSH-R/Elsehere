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
            case HOTEL, RETURN -> "the hotel";
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
    return new QuestTalk.Goals(journey.getStage(), state, instruction, List.copyOf(pending));
  }

  public void accept(Long userId, int expectedStage, String npcCode, JudgeResult.Intent intent) {
    if (!settings.enabled() || intent == null || intent.error() != null) {
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
    requireEnabled();
    return journeys
        .findById(userId)
        .map(this::response)
        .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
  }

  public Optional<FirstDayMissionResponse> visitIfActive(Long userId, String scene) {
    if (!settings.enabled()) {
      return Optional.empty();
    }
    if (!List.of("HOME", "TOWN", "HOTEL", "CAFE", "SHOP").contains(scene)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    return journeys
        .findForUpdate(userId)
        .filter(ShowcaseJourney::isActive)
        .map(
            journey -> {
              journey.visit(scene);
              return response(journey);
            });
  }

  public FirstDayMissionResponse start(Long userId) {
    requireEnabled();
    lockUserWithoutConversation(userId);
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .orElseGet(() -> journeys.save(ShowcaseJourney.start(userId)));
    journey.resume();
    return response(journey);
  }

  public FirstDayMissionResponse reset(Long userId) {
    requireEnabled();
    lockUserWithoutConversation(userId);
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .orElseGet(() -> journeys.save(ShowcaseJourney.start(userId)));
    journey.restart();
    return response(journey);
  }

  public FirstDayMissionResponse stop(Long userId) {
    requireEnabled();
    lockUserWithoutConversation(userId);
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
    journey.stop();
    return response(journey);
  }

  public FirstDayMissionResponse taxiArrival(Long userId, String destination) {
    requireEnabled();
    ShowcaseJourney journey =
        journeys
            .findForUpdate(userId)
            .filter(ShowcaseJourney::isActive)
            .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
    journey.arriveByTaxi(destination);
    return response(journey);
  }

  private void requireEnabled() {
    if (!settings.enabled()) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
  }

  private void lockUserWithoutConversation(Long userId) {
    users.findForUpdate(userId).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (conversations.findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE).isPresent()) {
      throw new BusinessException(ErrorCode.MISSION_NOT_READY);
    }
  }

  private Optional<ShowcaseJourney> active(Long userId) {
    return settings.enabled()
        ? journeys.findById(userId).filter(ShowcaseJourney::isActive)
        : Optional.empty();
  }

  @Transactional(readOnly = true)
  public Optional<QuestBoardResponse.Quest> card(Long userId, Map<String, String> names) {
    if (!settings.enabled()) {
      return Optional.empty();
    }
    ShowcaseJourney journey = journeys.findById(userId).orElse(null);
    List<QuestBoardResponse.Stop> stops = new ArrayList<>();
    for (ShowcaseStage stage : ShowcaseStage.values()) {
      int index = stage.ordinal();
      String place =
          switch (stage) {
            case HOTEL, RETURN -> "호텔";
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
              journey != null && index < journey.getStage(),
              journey != null && journey.isActive() && index == journey.getStage()));
    }
    String status =
        journey == null
            ? "AVAILABLE"
            : journey.isCompleted() ? "DONE" : journey.isActive() ? "IN_PROGRESS" : "AVAILABLE";
    return Optional.of(
        new QuestBoardResponse.Quest(
            ShowcaseStage.QUEST_ID,
            "비 오는 저녁, 햄버거 한 끼",
            "[시연] 우산을 빌려 택시로 햄버거 가게에 다녀오고, 같은 호텔 직원에게 감사 인사를 전해요.",
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
