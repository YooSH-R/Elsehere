package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import org.junit.jupiter.api.Test;

class ShowcaseQuestServiceTest {
  private final ShowcaseJourneyRepository journeys = mock(ShowcaseJourneyRepository.class);
  private final UserRepository users = mock(UserRepository.class);
  private final ConversationRepository conversations = mock(ConversationRepository.class);
  private final ShowcaseScene scene = mock(ShowcaseScene.class);
  private final FirstDayMissionRepository firstDay = mock(FirstDayMissionRepository.class);
  private final TownStoryRepository stories = mock(TownStoryRepository.class);

  // 공용 서버(시연 설정 꺼짐)에서는 모든 계정의 퀘스트 목록에 시연 카드가 보이지만, 시작하기 전에는 일반 진행 그대로다.
  @Test
  void publicServerShowsTheCardToEveryoneButKeepsNormalProgressBeforeStart() {
    ShowcaseQuestService service = service(false);

    assertThat(service.isShowcaseServer()).isFalse();
    assertThat(service.card(8L, Map.of()))
        .get()
        .extracting(QuestBoardResponse.Quest::status)
        .isEqualTo("AVAILABLE");
    assertThat(service.context(8L, "HOTEL_STAFF")).isEmpty();
    assertThat(service.activeStatus(8L)).isEmpty();
    assertThat(service.shownStatus(8L)).isEmpty();
    verifyNoInteractions(users, conversations, scene);
  }

  // 공용 서버에서는 시연을 시작한 계정만, 진행하는 동안 시연 목표와 상태를 받는다. 끝나면 일반 진행으로 돌아간다.
  @Test
  void publicServerAppliesTheShowcaseOnlyWhileTheAccountIsRunningIt() {
    ShowcaseJourney running = ShowcaseJourney.start(8L);
    when(journeys.findById(8L)).thenReturn(Optional.of(running));
    ShowcaseQuestService service = service(false);
    assertThat(service.shownStatus(8L)).isPresent();
    assertThat(service.context(8L, "HOTEL_STAFF")).isPresent();

    when(journeys.findById(8L)).thenReturn(Optional.of(completedJourney()));

    assertThat(service.shownStatus(8L)).isEmpty();
    assertThat(service.context(8L, "HOTEL_STAFF")).isEmpty();
    assertThat(service.activeStatus(8L)).isEmpty();
  }

  // 공용 서버에서도 누구나 시연을 시작할 수 있다. 처음부터 시작할 때만 대본 상태(NPC 관계·게임 시각)를 맞추고, 멈췄다 이어 할 때는 건드리지 않는다.
  @Test
  void anyoneCanStartAndOnlyAStartFromTheBeginningSetsTheScriptedScene() {
    when(journeys.findForUpdate(8L)).thenReturn(Optional.empty());
    when(journeys.save(org.mockito.ArgumentMatchers.any()))
        .thenAnswer(invocation -> invocation.getArgument(0));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));
    ShowcaseQuestService service = service(false);
    service.start(8L);
    org.mockito.ArgumentCaptor<ShowcaseJourney> saved =
        org.mockito.ArgumentCaptor.forClass(ShowcaseJourney.class);
    verify(journeys).save(saved.capture());
    ShowcaseJourney journey = saved.getValue();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));

    journey.accept(0, "HOTEL_STAFF", List.of("ask_for_taxi"));
    journey.stop();
    service.start(8L);

    verify(scene).setUp(8L);
    assertThat(journey.isActive()).isTrue();
    assertThat(journey.current()).isEqualTo(ShowcaseStage.TAXI);
  }

  // 시연은 주문에서 끝난다. 주문을 마친 뒤 카드를 다시 누르면 처음부터 시작해 대본 상태를 다시 맞춘다.
  @Test
  void startingAfterTheOrderBeginsAgainFromTheHotelWithTheScriptedScene() {
    ShowcaseJourney journey = completedJourney();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    service(false).start(8L);

    assertThat(journey.current()).isEqualTo(ShowcaseStage.HOTEL);
    assertThat(journey.isActive()).isTrue();
    assertThat(journey.hasUmbrella()).isFalse();
    assertThat(journey.weather()).isEqualTo("RAIN");
    verify(scene).setUp(8L);
  }

  // 공용 서버에서는 시연을 진행하는 동안에만 다른 퀘스트를 잠근다. 시작 전이나 끝난 뒤에는 일반 퀘스트를 그대로 둔다.
  @Test
  void publicServerLocksOtherQuestsOnlyWhileTheShowcaseRuns() {
    ShowcaseQuestService service = service(false);

    assertThat(service.locksOtherQuests(questWithStatus("AVAILABLE"))).isFalse();
    assertThat(service.locksOtherQuests(questWithStatus("IN_PROGRESS"))).isTrue();
    assertThat(service(true).locksOtherQuests(questWithStatus("AVAILABLE"))).isTrue();
    assertThat(service(true).locksOtherQuests(questWithStatus("DONE"))).isFalse();
  }

  private static QuestBoardResponse.Quest questWithStatus(String status) {
    return new QuestBoardResponse.Quest(
        ShowcaseStage.QUEST_ID, "비 오는 저녁, 햄버거 한 끼", "시연", 0, status, List.of());
  }

  @Test
  void otherNpcNeverReceivesTheHotelOrBurgerStaffInstructions() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));
    QuestTalk talk = service(true).context(8L, "DINER_STAFF").orElseThrow();
    assertThat(talk).isInstanceOf(QuestTalk.Guide.class);
    assertThat(talk.instruction()).contains("hotel", "own role").doesNotContain("lend an umbrella");
  }

  // 지금 단계 장소에 함께 있는 시연 인물(호텔 보조 직원 Owen·Diego, 버거 가게의 John)은 이미 와 있는 곳으로 가라고 하지 않고 그곳의 담당자를 가리킨다.
  @Test
  void peopleAlreadyAtTheStagePlacePointToItsOwnerInsteadOfSendingTheLearnerThere() {
    when(journeys.findById(8L)).thenReturn(Optional.of(ShowcaseJourney.start(8L)));
    for (String npc : List.of("HOTEL_RECEPTIONIST", "HOTEL_BELLHOP")) {
      assertThat(service(false).context(8L, npc).orElseThrow().instruction())
          .contains("Jake at the front desk", "own role")
          .doesNotContain("next stop is the hotel");
    }

    when(journeys.findById(8L)).thenReturn(Optional.of(atBurgerShop()));

    assertThat(service(false).context(8L, "TOWN_JOHN_PARK").orElseThrow().instruction())
        .contains("Rosa at the order counter", "own role")
        .doesNotContain("next stop is Maple Burger");
  }

  @Test
  void taxiKeepsTheNormalDestinationJudgeAndOrderOnlyUsesItsOwnGoals() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", java.util.List.of("ask_for_taxi"));
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));
    assertThat(service(true).context(8L, "TAXI_DRIVER").orElseThrow().goals()).isEmpty();
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    QuestTalk.Goals order =
        service(true).context(8L, "DINER_STAFF").orElseThrow().goals().orElseThrow();
    assertThat(order.remainingGoals())
        .containsExactly("order_burger", "customize_burger", "add_sides_drink", "choose_dining");
    assertThat(order.instruction()).contains("Both for here and to go are valid", "7 coins");
  }

  // 호텔 직원은 택시 부탁을 기다려 호텔 앞 택시와 우산을 안내하고, 학습자의 우산 반환 약속을 목표로 기다리지 않는다.
  @Test
  void hotelStaffOnlyWaitsForTheTaxiRequest() {
    when(journeys.findById(8L)).thenReturn(Optional.of(ShowcaseJourney.start(8L)));

    QuestTalk.Goals hotel =
        service(true).context(8L, "HOTEL_STAFF").orElseThrow().goals().orElseThrow();

    assertThat(hotel.remainingGoals()).containsExactly("ask_for_taxi");
    assertThat(hotel.instruction())
        .contains("a taxi is waiting right outside", "lend them an umbrella")
        .doesNotContain("bring it back");
  }

  // 대본과 달리 길을 물어도 호텔 단계가 막히지 않도록, 택시 부탁과 길 묻기를 같은 목표로 인정한다.
  @Test
  void hotelGoalAlsoCountsAskingTheWayToTheBurgerShop() {
    ShowcaseStage.Goal goal = ShowcaseStage.HOTEL.goals().get(0);

    assertThat(goal.description()).contains("taxi", "where the burger shop or Maple Burger is");
  }

  // 단골을 기억하면 인사에서 늘 시키던 메뉴를 물을 수 있고, 학습자가 다른 걸 고르면 그 주문을 받는다.
  @Test
  void burgerStaffMayOfferARegularsUsualButTakesANewChoice() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", java.util.List.of("ask_for_taxi"));
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));

    QuestTalk.Goals order =
        service(true).context(8L, "DINER_STAFF").orElseThrow().goals().orElseThrow();

    assertThat(order.instruction())
        .contains("their usual", "take the new order")
        .doesNotContain("Offer no completed order");
  }

  // 주문 단계는 세부 목표가 넷이라, 하나를 마치면 직원이 순서대로 다음 목표를 묻는 질문으로 이끈다.
  @Test
  void burgerStaffAsksAboutTheNextPendingGoalAfterOneIsDone() {
    when(journeys.findById(8L)).thenReturn(Optional.of(atBurgerShop()));
    QuestTalk order = service(true).context(8L, "DINER_STAFF").orElseThrow();

    String afterBurger =
        order.replyInstruction(new JudgeResult.Intent(null, List.of("order_burger"), null));
    String afterBurgerAndSides =
        order.replyInstruction(
            new JudgeResult.Intent(null, List.of("order_burger", "add_sides_drink"), null));

    assertThat(afterBurger)
        .contains("ask one short question that leads the learner to customize_burger")
        .doesNotContain("Do not ask about, hint at or offer a remaining objective");
    assertThat(afterBurgerAndSides)
        .contains("ask one short question that leads the learner to customize_burger");
    assertThat(order.instruction())
        .contains("ask about the next missing choice in this order")
        .doesNotContain("Ask at most one short follow-up");
  }

  // 목표가 하나인 호텔 단계는 그대로 학습자가 먼저 택시를 부탁하기를 기다린다.
  @Test
  void hotelStaffStillWaitsInsteadOfLeadingToTheTaxiRequest() {
    when(journeys.findById(8L)).thenReturn(Optional.of(ShowcaseJourney.start(8L)));
    QuestTalk hotel = service(true).context(8L, "HOTEL_STAFF").orElseThrow();

    String reply = hotel.replyInstruction(new JudgeResult.Intent(null, List.of(), null));

    assertThat(reply)
        .contains("Do not ask about, hint at or offer a remaining objective")
        .doesNotContain("ask one short question that leads the learner to");
  }

  // 시연 서버에서는 시작 전에도 시연 퀘스트를 보여, 다른 퀘스트가 진행 중으로 보이지 않는다. 눌러야 시작된다.
  @Test
  void showcaseServerShowsTheDemoNotStartedUntilItIsPressed() {
    when(journeys.findById(8L)).thenReturn(Optional.empty());
    when(journeys.findForUpdate(8L)).thenReturn(Optional.empty());
    ShowcaseQuestService enabled = service(true);

    for (FirstDayMissionResponse shown :
        List.of(enabled.shownStatus(8L).orElseThrow(), enabled.visit(8L, "HOTEL").orElseThrow())) {
      assertThat(shown.quest()).isEqualTo("SHOWCASE_RAINY_EVENING");
      assertThat(shown.started()).isFalse();
    }
    verify(journeys, never()).save(any());
    assertThat(service(false).shownStatus(8L)).isEmpty();
    assertThat(service(false).visit(8L, "HOTEL")).isEmpty();
  }

  private ShowcaseQuestService service(boolean enabled) {
    return new ShowcaseQuestService(
        new ShowcaseSettings(enabled), journeys, users, conversations, scene, firstDay, stories);
  }

  // 시연 때문에 잠긴 퀘스트에 보일 이유. 공용 서버는 시연을 마치거나 그만두면 열리고, 시연 전용 서버는 시연을 마쳐야 열린다.
  @Test
  void theLockReasonSaysTheShowcaseLockedTheQuest() {
    assertThat(service(false).lockReason()).contains("시연 퀘스트", "그만두면");
    assertThat(service(true).lockReason()).contains("시연 서버", "마쳐야").doesNotContain("그만두면");
  }

  // 시연을 시작하면 하던 첫날 퀘스트는 취소되어 시작 전으로 돌아간다. 시연을 끝내도 이어지지 않는다.
  @Test
  void startingCancelsAnUnfinishedFirstDayQuest() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, FirstDayStage.values()[0].npc(), FirstDayStage.values()[0].intentIds());
    when(firstDay.findForUpdate(8L)).thenReturn(Optional.of(mission));
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(ShowcaseJourney.notStarted(8L)));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    service(false).start(8L);

    verify(firstDay).delete(mission);
  }

  // 시연을 시작하면 하던 마을 퀘스트는 취소되어 그 퀘스트의 처음에서 시작을 기다린다.
  @Test
  void startingCancelsATownQuestInProgress() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast", "explain_spill"));
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(ShowcaseJourney.notStarted(8L)));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    service(false).start(8L);

    assertThat(story.getStage()).isZero();
    assertThat(story.isPaused()).isTrue();
  }

  // 끝낸 첫날 퀘스트는 취소하지 않는다. 받은 보상과 다음 퀘스트가 그대로다.
  @Test
  void startingKeepsAFinishedFirstDayQuest() {
    FirstDayMission mission = mock(FirstDayMission.class);
    when(mission.isCompleted()).thenReturn(true);
    when(firstDay.findForUpdate(8L)).thenReturn(Optional.of(mission));
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(ShowcaseJourney.notStarted(8L)));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    service(false).start(8L);

    verify(firstDay, never()).delete(any());
  }

  // 다른 퀘스트를 시작하면 하다 멈춘 시연은 취소되어 처음으로 돌아가고, 계정도 시작 전 상태로 돌아간다.
  @Test
  void cancellingAnUnfinishedShowcaseRestartsItAndPutsTheAccountBack() {
    ShowcaseJourney journey = atBurgerShop();
    journey.stop();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));

    service(false).cancelUnfinished(8L);

    assertThat(journey.getStage()).isZero();
    assertThat(journey.isActive()).isFalse();
    assertThat(journey.isUmbrellaBorrowed()).isFalse();
    verify(scene).leave(8L);
  }

  // 끝낸 시연은 취소할 것이 없다. 다시 누르면 처음부터 시작하는 것도 그대로다.
  @Test
  void cancellingLeavesAFinishedShowcaseAlone() {
    ShowcaseJourney journey = completedJourney();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));

    service(false).cancelUnfinished(8L);

    assertThat(journey.isCompleted()).isTrue();
    verifyNoInteractions(scene);
  }

  // 주문을 마치면 시연이 끝난다. 호텔로 돌아가 감사 인사를 하는 단계는 없다.
  @Test
  void completingTheOrderCompletesTheQuestAndKeepsShowcaseContextForTheNextTurn() {
    ShowcaseJourney journey = atBurgerShop();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));
    ShowcaseQuestService service = service(true);

    service.accept(
        8L, 2, "DINER_STAFF", new JudgeResult.Intent(null, ShowcaseStage.ORDER.goalIds(), null));

    assertThat(service.status(8L).completed()).isTrue();
    assertThat(service.status(8L).steps())
        .extracting(FirstDayMissionResponse.Step::status)
        .containsExactly("DONE", "DONE", "DONE");
    assertThat(service.activeStatus(8L))
        .hasValueSatisfying(state -> assertThat(state.completed()).isTrue());
    assertThat(service.context(8L, "HOTEL_STAFF").orElseThrow().instruction())
        .contains("rainy-evening trip is complete", "without starting check-in");
    service.accept(
        8L, 2, "DINER_STAFF", new JudgeResult.Intent(null, ShowcaseStage.ORDER.goalIds(), null));
    assertThat(journey.getStage()).isEqualTo(ShowcaseStage.values().length);
    assertThat(journey.balance()).isEqualTo(93);
  }

  // 퀘스트 카드와 목록에 우산 감사 단계가 보이지 않는다.
  @Test
  void questNoLongerShowsTheUmbrellaThanksStep() {
    when(journeys.findById(8L)).thenReturn(Optional.of(ShowcaseJourney.start(8L)));
    ShowcaseQuestService service = service(true);

    assertThat(service.status(8L).steps())
        .extracting(FirstDayMissionResponse.Step::npcCode)
        .containsExactly("HOTEL_STAFF", "TAXI_DRIVER", "DINER_STAFF");
    QuestBoardResponse.Quest card = service.card(8L, Map.of()).orElseThrow();
    assertThat(card.stops()).hasSize(3);
    assertThat(card.description()).doesNotContain("감사");
  }

  @Test
  void completedCardIsAvailableWithoutPreviousStopsAndDoesNotResetTheJourney() {
    ShowcaseJourney journey = completedJourney();
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));

    QuestBoardResponse.Quest card = service(true).card(8L, Map.of()).orElseThrow();

    assertThat(card.status()).isEqualTo("AVAILABLE");
    assertThat(card.stops())
        .hasSize(3)
        .allSatisfy(
            stop -> {
              assertThat(stop.done()).isFalse();
              assertThat(stop.current()).isFalse();
            });
    assertThat(journey.isCompleted()).isTrue();
    assertThat(journey.isActive()).isTrue();
    assertThat(journey.isMealPaid()).isTrue();
    assertThat(journey.weather()).isEqualTo("CLEAR");
    FirstDayMissionResponse state = service(true).activeStatus(8L).orElseThrow();
    assertThat(state.started()).isTrue();
    assertThat(state.completed()).isTrue();
    assertThat(state.direction()).contains("대화 노트", "햄버거 주문", "다시 연습");
  }

  @Test
  void startingACompletedJourneyResetsUmbrellaPaymentTaxiAndWeather() {
    ShowcaseJourney journey = completedJourney();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    FirstDayMissionResponse state = service(true).start(8L);

    assertThat(state.started()).isTrue();
    assertThat(state.completed()).isFalse();
    assertThat(state.stage()).isZero();
    assertThat(state.scene()).isEqualTo("HOTEL");
    assertThat(state.weather()).isEqualTo("RAIN");
    assertThat(state.raining()).isTrue();
    assertThat(state.balance()).isEqualTo(100);
    assertThat(state.lastPurchase()).isNull();
    assertThat(state.steps())
        .extracting(FirstDayMissionResponse.Step::status)
        .containsExactly("CURRENT", "PENDING", "PENDING");
    assertThat(journey.isUmbrellaBorrowed()).isFalse();
    assertThat(journey.isMealPaid()).isFalse();
    assertThat(journey.isTaxiRequested()).isFalse();
    assertThat(journey.getObjectives()).isZero();
  }

  @Test
  void startDuringAnUnfinishedJourneyResumesWithoutLosingProgress() {
    ShowcaseJourney journey = atBurgerShop();
    journey.accept(2, "DINER_STAFF", List.of("order_burger"));
    journey.stop();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    FirstDayMissionResponse state = service(true).start(8L);

    assertThat(state.started()).isTrue();
    assertThat(state.stage()).isEqualTo(2);
    assertThat(state.steps().get(2).goals())
        .extracting(FirstDayMissionResponse.Goal::completed)
        .containsExactly(true, false, false, false);
    assertThat(state.weather()).isEqualTo("RAIN");
    assertThat(journey.isUmbrellaBorrowed()).isTrue();
    verify(scene, never()).setUp(8L);
  }

  @Test
  void replayCannotResetACompletedJourneyWhileTheConversationIsStillActive() {
    ShowcaseJourney journey = completedJourney();
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));
    when(conversations.findFirstByUserIdAndStatus(8L, ConversationStatus.ACTIVE))
        .thenReturn(Optional.of(mock(Conversation.class)));

    assertThatThrownBy(() -> service(true).start(8L)).isInstanceOf(BusinessException.class);
    assertThat(journey.isCompleted()).isTrue();
    assertThat(journey.isMealPaid()).isTrue();
    verifyNoInteractions(journeys);
  }

  @Test
  void completedShowcaseCanReplayButOtherUnstartedQuestsStayLocked() {
    ShowcaseJourney journey = completedJourney();
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));
    QuestBoardService board =
        new QuestBoardService(
            mock(FirstDayMissionRepository.class),
            mock(TownStoryRepository.class),
            mock(NpcRepository.class),
            mock(com.elsehere.favor.repository.FavorRepository.class),
            service(true));

    assertThat(board.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("AVAILABLE", "LOCKED", "LOCKED", "LOCKED");
    assertThat(journey.isCompleted()).isTrue();
  }

  // 시연을 멈추면 계정을 시작 전 상태로 돌려놓고, 이어 하면 멈춘 때의 시연 상태로 되돌린다.
  @Test
  void stoppingPutsTheAccountBackAndResumingBringsTheShowcaseBack() {
    ShowcaseJourney journey = atBurgerShop();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));
    ShowcaseQuestService service = service(false);

    service.stop(8L);

    verify(scene).pause(8L);
    verify(scene, never()).resume(8L);

    service.start(8L);

    verify(scene).resume(8L);
    verify(scene, never()).setUp(8L);
  }

  // 처음으로 되돌리면 계정도 시작 전 상태로 돌아간다.
  @Test
  void resettingPutsTheAccountBack() {
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(atBurgerShop()));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    service(false).reset(8L);

    verify(scene).leave(8L);
  }

  // 공용 서버에서는 주문을 마쳐 시연이 끝나면 계정을 시작 전 상태로 돌려놓는다. 진행 중이거나 시연 전용 서버면 그대로 둔다.
  @Test
  void aFinishedShowcaseOnThePublicServerPutsTheAccountBack() {
    when(journeys.findById(8L)).thenReturn(Optional.of(atBurgerShop()));
    assertThat(service(false).leaveIfFinished(8L)).isFalse();

    when(journeys.findById(8L)).thenReturn(Optional.of(completedJourney()));
    assertThat(service(true).leaveIfFinished(8L)).isFalse();
    verifyNoInteractions(scene);

    when(scene.leave(8L)).thenReturn(true);
    assertThat(service(false).leaveIfFinished(8L)).isTrue();
  }

  // 끝낸 대화를 닫지 않고 나갔어도, 다음에 장소에 들어올 때 계정을 시작 전 상태로 돌려놓는다.
  @Test
  void visitingAPlaceAfterTheShowcaseFinishedPutsTheAccountBack() {
    when(journeys.findById(8L)).thenReturn(Optional.of(completedJourney()));

    assertThat(service(false).visit(8L, "TOWN")).isEmpty();

    verify(scene).leave(8L);
  }

  private static ShowcaseJourney atBurgerShop() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    return journey;
  }

  private static ShowcaseJourney completedJourney() {
    ShowcaseJourney journey = atBurgerShop();
    journey.accept(2, "DINER_STAFF", ShowcaseStage.ORDER.goalIds());
    return journey;
  }
}
