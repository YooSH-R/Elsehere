package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.dto.PracticePlaceListResponse;
import com.elsehere.mission.dto.PracticeProgressResponse;
import com.elsehere.mission.entity.PracticePlace;
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 장소 연습. 가게 직원과 고른 상황 하나만 연습하며, 이야기 퀘스트와 따로 처음부터 모두 열려 있고 몇 번이든 다시 할 수 있다. 코인·도장 없이 끝낸 기록만 남긴다.
 *
 * <p>목표 진행은 연습 대화(conversations.practice_*)에 저장한다. 다시 하면 새 대화라 목표를 처음부터 판정하고, 목록의 완료는 목표를 모두 이룬 대화가
 * 하나라도 있는지로 본다. 목표 판정과 직원 지시는 이야기 퀘스트와 같은 {@link FirstDayMissionService.Context}로 만든다.
 */
@Service
@RequiredArgsConstructor
public class PlacePracticeService {

  // 직원의 평소 사실(가격·서비스 등)과 상황의 사실이 다르면 이 대화에서는 상황을 따르게 한다.
  private static final String SITUATION =
      "Situation for this conversation (use these facts when they differ from your usual ones): ";
  private static final String ALL_DONE =
      " The learner has already done everything for this situation. Do not start a new request; close politely.";

  private final ConversationRepository conversations;
  private final NpcRepository npcs;
  private final Clock clock;

  /** PRC-01. 이름·역할·장소 같은 직원 정보는 NPC 시드에서 읽는다. */
  @Transactional(readOnly = true)
  public PracticePlaceListResponse places(Long userId) {
    Set<String> finished = new HashSet<>(conversations.findFinishedPracticeScenarios(userId));
    List<PracticePlaceListResponse.Place> places = new ArrayList<>();
    for (PracticePlace place : PracticePlace.values()) {
      Npc npc =
          npcs.findByCode(place.npcCode())
              .orElseThrow(
                  () ->
                      new IllegalStateException("Practice NPC is not seeded: " + place.npcCode()));
      List<PracticeScenario> scenarios = PracticeScenario.of(place);
      List<PracticePlaceListResponse.Scenario> items =
          scenarios.stream()
              .map(scenario -> listItem(scenario, finished.contains(scenario.name())))
              .toList();
      int doneCount = (int) items.stream().filter(PracticePlaceListResponse.Scenario::done).count();
      places.add(
          new PracticePlaceListResponse.Place(
              place.name(),
              place.title(),
              scenarios.get(0).title() + " · " + scenarios.get(1).title(),
              npc.getCode(),
              npc.getName(),
              npc.getRoleLabel(),
              npc.getScene().getCode(),
              doneCount,
              items.size(),
              items));
    }
    return new PracticePlaceListResponse(places);
  }

  /** PRC-03. 이 사용자의 이 상황 연습 대화만 읽는다. 다른 사용자나 다른 상황의 대화는 있는지 드러내지 않도록 NOT_FOUND다. */
  @Transactional(readOnly = true)
  public PracticeProgressResponse progress(Long userId, String scenarioId, Long conversationId) {
    PracticeScenario scenario = scenario(scenarioId);
    Conversation conversation =
        conversations
            .findByIdAndUserId(conversationId, userId)
            .filter(found -> scenario.name().equals(found.getPracticeScenario()))
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    List<String> completed = conversation.getPracticeCompletedGoals();
    List<PracticeProgressResponse.Goal> goals =
        scenario.goals().stream()
            .map(
                goal ->
                    new PracticeProgressResponse.Goal(
                        goal.id(), goal.title(), goal.hint(), completed.contains(goal.id())))
            .toList();
    return new PracticeProgressResponse(
        scenario.name(), goals, conversation.getPracticeDoneAt() != null);
  }

  /** API의 scenarioId에 맞는 상황. 없으면 NOT_FOUND다. */
  public PracticeScenario scenario(String scenarioId) {
    return PracticeScenario.find(scenarioId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 장소 연습 대화에서 판정할 목표와 직원 지시. 장소 연습이 아닌 대화는 빈 값이다. */
  public Optional<FirstDayMissionService.Context> context(Conversation conversation) {
    if (!conversation.isPractice()) {
      return Optional.empty();
    }
    return Optional.of(
        context(
            scenario(conversation.getPracticeScenario()),
            conversation.getPracticeCompletedGoals()));
  }

  /**
   * 첫 인사와 발화 판정에 쓰는 상황. 이야기 퀘스트처럼 남은 목표만 판정하고, 직원은 학습자가 먼저 요청하기를 기다린다.
   *
   * @param completedGoalIds 이 대화에서 이미 이룬 목표. 시작할 때는 빈 목록이다
   */
  public FirstDayMissionService.Context context(
      PracticeScenario scenario, List<String> completedGoalIds) {
    String situation = SITUATION + scenario.instruction();
    List<PracticeScenario.Goal> remaining = scenario.remainingGoals(completedGoalIds);
    // 목표를 모두 이루면 대화도 끝나므로 보통 오지 않는다. 판정할 목표가 없어 워커에 빈 의도 목록을 보내지 않는다.
    if (remaining.isEmpty()) {
      return new FirstDayMissionService.Context(
          scenario.number(), null, situation + ALL_DONE, List.of());
    }
    List<String> remainingIds = remaining.stream().map(PracticeScenario.Goal::id).toList();
    WorkerState state =
        new WorkerState(
            "practice_" + scenario.name(),
            scenario.instruction() + FirstDayMissionService.QUEST_EVIDENCE,
            remaining.stream()
                .map(goal -> new WorkerState.Intent(goal.id(), goal.description()))
                .toList(),
            true);
    return new FirstDayMissionService.Context(
        scenario.number(),
        state,
        situation
            + " Objectives pending before this turn: "
            + String.join(",", remainingIds)
            + "."
            + FirstDayMissionService.LEARNER_LEADS,
        remainingIds);
  }

  /**
   * 판정한 의도 중 이 상황의 목표만 이 대화에 더하고, 목표를 모두 이루면 완료 시각을 남긴다. 성공한 턴을 저장하는 트랜잭션에서 잠근 대화로 부른다. 판정은 허용된 의도의
   * 제안이다.
   */
  public void accept(Conversation conversation, JudgeResult.Intent intent) {
    if (!conversation.isPractice()
        || intent == null
        || intent.error() != null
        || intent.ids() == null) {
      return;
    }
    PracticeScenario scenario = scenario(conversation.getPracticeScenario());
    List<String> alreadyCompleted = conversation.getPracticeCompletedGoals();
    List<String> completed =
        scenario.goals().stream()
            .map(PracticeScenario.Goal::id)
            .filter(id -> alreadyCompleted.contains(id) || intent.ids().contains(id))
            .toList();
    conversation.recordPracticeGoals(completed);
    if (completed.size() == scenario.goals().size()) {
      conversation.finishPractice(clock.instant());
    }
  }

  private static PracticePlaceListResponse.Scenario listItem(
      PracticeScenario scenario, boolean done) {
    return new PracticePlaceListResponse.Scenario(
        scenario.name(),
        scenario.number(),
        scenario.title(),
        scenario.description(),
        done,
        scenario.goals().stream()
            .map(goal -> new PracticePlaceListResponse.Goal(goal.id(), goal.title(), goal.hint()))
            .toList());
  }
}
