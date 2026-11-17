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
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.entity.ShowcaseStage;
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

  // 시연 대본은 주문과 다시 연습에서 끝나 Jake 감사(귀환 단계)를 하지 않는다. 주문을 마친 뒤 카드를 다시 누르면 처음부터 시작해 대본 상태를 다시 맞춘다.
  @Test
  void startingAfterTheOrderBeginsAgainFromTheHotelWithTheScriptedScene() {
    ShowcaseJourney journey = readyToThankJake();
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
        new ShowcaseSettings(enabled), journeys, users, conversations, scene);
  }

  @Test
  void completingTheThanksKeepsCompletionAndShowcaseContextForTheNextTurn() {
    ShowcaseJourney journey = readyToThankJake();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));
    ShowcaseQuestService service = service(true);

    service.accept(
        8L, 3, "HOTEL_STAFF", new JudgeResult.Intent(null, ShowcaseStage.RETURN.goalIds(), null));

    assertThat(service.status(8L).completed()).isTrue();
    assertThat(service.activeStatus(8L))
        .hasValueSatisfying(state -> assertThat(state.completed()).isTrue());
    assertThat(service.context(8L, "HOTEL_STAFF").orElseThrow().instruction())
        .contains("rainy-evening trip is complete", "without starting check-in");
    service.accept(
        8L, 3, "HOTEL_STAFF", new JudgeResult.Intent(null, ShowcaseStage.RETURN.goalIds(), null));
    assertThat(journey.getStage()).isEqualTo(ShowcaseStage.values().length);
    assertThat(journey.balance()).isEqualTo(93);
  }

  @Test
  void completedCardIsAvailableWithoutPreviousStopsAndDoesNotResetTheJourney() {
    ShowcaseJourney journey = completedJourney();
    when(journeys.findById(8L)).thenReturn(Optional.of(journey));

    QuestBoardResponse.Quest card = service(true).card(8L, Map.of()).orElseThrow();

    assertThat(card.status()).isEqualTo("AVAILABLE");
    assertThat(card.stops())
        .hasSize(4)
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
        .containsExactly("CURRENT", "PENDING", "PENDING", "PENDING");
    assertThat(journey.isUmbrellaBorrowed()).isFalse();
    assertThat(journey.isMealPaid()).isFalse();
    assertThat(journey.isTaxiRequested()).isFalse();
    assertThat(journey.getObjectives()).isZero();
  }

  @Test
  void startDuringAnUnfinishedJourneyResumesWithoutLosingProgress() {
    ShowcaseJourney journey = readyToThankJake();
    journey.stop();
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));

    FirstDayMissionResponse state = service(true).start(8L);

    assertThat(state.started()).isTrue();
    assertThat(state.stage()).isEqualTo(3);
    assertThat(state.balance()).isEqualTo(93);
    assertThat(state.weather()).isEqualTo("CLEAR");
    assertThat(journey.isUmbrellaBorrowed()).isTrue();
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

  private static ShowcaseJourney readyToThankJake() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    journey.accept(2, "DINER_STAFF", ShowcaseStage.ORDER.goalIds());
    journey.visit("HOTEL");
    return journey;
  }

  private static ShowcaseJourney completedJourney() {
    ShowcaseJourney journey = readyToThankJake();
    journey.accept(3, "HOTEL_STAFF", ShowcaseStage.RETURN.goalIds());
    return journey;
  }
}
