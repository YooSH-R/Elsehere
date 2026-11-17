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

  @Test
  void disabledFeatureDoesNotReadOrOverrideNormalProgress() {
    ShowcaseQuestService service = service(false);
    assertThat(service.context(8L, "HOTEL_STAFF")).isEmpty();
    assertThat(service.activeStatus(8L)).isEmpty();
    assertThat(service.card(8L, Map.of())).isEmpty();
    verifyNoInteractions(journeys, users, conversations);
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
    journey.accept(0, "HOTEL_STAFF", java.util.List.of("ask_burger_location"));
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

  // 호텔 직원은 길과 택시·우산만 안내하고, 학습자의 우산 반환 약속을 목표로 기다리지 않는다.
  @Test
  void hotelStaffOnlyWaitsForTheWayToTheBurgerShop() {
    when(journeys.findById(8L)).thenReturn(Optional.of(ShowcaseJourney.start(8L)));

    QuestTalk.Goals hotel =
        service(true).context(8L, "HOTEL_STAFF").orElseThrow().goals().orElseThrow();

    assertThat(hotel.remainingGoals()).containsExactly("ask_burger_location");
    assertThat(hotel.instruction())
        .contains("lend them an umbrella")
        .doesNotContain("bring it back");
  }

  // 단골을 기억하면 인사에서 늘 시키던 메뉴를 물을 수 있고, 학습자가 다른 걸 고르면 그 주문을 받는다.
  @Test
  void burgerStaffMayOfferARegularsUsualButTakesANewChoice() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", java.util.List.of("ask_burger_location"));
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
    return new ShowcaseQuestService(new ShowcaseSettings(enabled), journeys, users, conversations);
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
            mock(FirstDayMissionRepository.class), mock(TownStoryRepository.class),
            mock(NpcRepository.class), service(true));

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
