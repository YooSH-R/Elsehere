package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.user.repository.UserRepository;
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

  private ShowcaseQuestService service(boolean enabled) {
    return new ShowcaseQuestService(new ShowcaseSettings(enabled), journeys, users, conversations);
  }
}
