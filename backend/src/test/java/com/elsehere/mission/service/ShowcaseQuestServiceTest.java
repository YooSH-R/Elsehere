package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.user.repository.UserRepository;
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
    journey.accept(
        0, "HOTEL_STAFF", java.util.List.of("ask_burger_location", "promise_umbrella_return"));
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
}
