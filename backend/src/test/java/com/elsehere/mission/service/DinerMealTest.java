package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.Coins;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.Test;

class DinerMealTest {
  @Test
  void receiptRejectsAnotherSceneOrAnActiveConversation() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast"));
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));
    FirstDayMission location = mock(FirstDayMission.class);
    when(firstDays.findById(8L)).thenReturn(Optional.of(location));
    when(location.getScene()).thenReturn("HOTEL");
    assertThatThrownBy(() -> service.receiveDinerMeal(8L)).isInstanceOf(BusinessException.class);
    when(location.getScene()).thenReturn("TOWN");
    when(conversations.findFirstByUserIdAndStatus(
            8L, com.elsehere.conversation.entity.ConversationStatus.ACTIVE))
        .thenReturn(Optional.of(mock(com.elsehere.conversation.entity.Conversation.class)));
    assertThatThrownBy(() -> service.receiveDinerMeal(8L)).isInstanceOf(BusinessException.class);
    assertThat(story.canWaitForDinerMeal()).isTrue();
  }

  private final TownStoryRepository stories = mock(TownStoryRepository.class);
  private final FirstDayMissionRepository firstDays = mock(FirstDayMissionRepository.class);
  private final ConversationRepository conversations = mock(ConversationRepository.class);
  private final TownStoryService service =
      new TownStoryService(stories, firstDays, mock(NpcRepository.class), conversations);

  @Test
  void orderWaitReceiveAndSpillAreSeparateAndReceiptIsIdempotent() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));
    FirstDayMission firstDay = mock(FirstDayMission.class);
    when(firstDay.getScene()).thenReturn("TOWN");
    when(firstDays.findById(8L)).thenReturn(Optional.of(firstDay));
    var opening = service.context(8L, "DINER_STAFF").orElseThrow();
    assertThat(opening.goals().orElseThrow().state().intents())
        .extracting(intent -> intent.id())
        .containsExactly("order_breakfast");
    assertThatThrownBy(() -> service.receiveDinerMeal(8L)).isInstanceOf(BusinessException.class);
    service.accept(
        8L,
        0,
        "DINER_STAFF",
        new JudgeResult.Intent(null, List.of("order_breakfast", "explain_spill"), null));
    assertThat(story.getStage()).isZero();
    assertThat(story.hasGoal(1)).isFalse();
    assertThat(service.status(8L).canRest()).isTrue();
    assertThat(
            service
                .context(8L, "DINER_STAFF")
                .orElseThrow()
                .goals()
                .orElseThrow()
                .state()
                .intents())
        .isEmpty();
    int balance = Coins.balance(firstDay, story);
    service.receiveDinerMeal(8L);
    service.receiveDinerMeal(8L);
    assertThat(service.status(8L).canRest()).isFalse();
    assertThat(service.status(8L).steps().getFirst().goals()).hasSize(2);
    assertThat(service.status(8L).steps().getFirst().goals().get(1).completed()).isFalse();
    assertThat(Coins.balance(firstDay, story)).isEqualTo(balance);
    assertThat(
            service
                .context(8L, "DINER_STAFF")
                .orElseThrow()
                .goals()
                .orElseThrow()
                .state()
                .intents())
        .extracting(intent -> intent.id())
        .containsExactly("explain_spill");
    service.accept(
        8L, 0, "DINER_STAFF", new JudgeResult.Intent(null, List.of("explain_spill"), null));
    assertThat(story.getStage()).isEqualTo(1);
  }
}
