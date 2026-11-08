package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.RETURNS_DEEP_STUBS;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.service.WorkerLines;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.NpcTravelContext;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.GameClockService;
import com.elsehere.world.service.GameTime;
import jakarta.validation.Validation;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class DialogueTravelContextTest {
  @Test
  void everyWorkerLookupCarriesTheSavedJourneyAndLegacyConversationsStillWork() {
    NpcRepository repository = mock(NpcRepository.class);
    WorkerNpcs workers = mock(WorkerNpcs.class);
    GameClockService clock = mock(GameClockService.class);
    Npc entity = mock(Npc.class, RETURNS_DEEP_STUBS);
    when(repository.findById(2L)).thenReturn(Optional.of(entity));
    when(entity.getCode()).thenReturn("CAFE_STAFF");
    when(entity.getName()).thenReturn("Mia");
    when(entity.getScene().getCode()).thenReturn("CAFE");
    when(entity.getScene().isOpen()).thenReturn(true);
    when(workers.of(entity))
        .thenReturn(new WorkerNpc("CAFE_STAFF", "Mia", "barista", "menu", "cafe", "voice", 1));
    when(clock.currentTime(1L)).thenReturn(GameTime.ofPlaySeconds(0));
    DialogueContexts contexts =
        new DialogueContexts(
            repository,
            mock(UserRepository.class),
            mock(TurnRepository.class),
            workers,
            mock(WorkerLines.class),
            clock);
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, Instant.EPOCH);
    assertThat(contexts.npcOf(conversation).workerNpc().scene()).doesNotContain("Movement facts");
    String facts =
        new NpcTravelContext(NpcTravelContext.Phase.GOING_TO_WORK, 80, 35, 190, 0).describe();
    conversation.recordNpcTravelSituation(facts);
    assertThat(contexts.npcOf(conversation).workerNpc().scene())
        .contains(facts, "Current game time");
    assertThat(contexts.npcOf(conversation).workerNpc().scene()).contains(facts);
  }

  @Test
  void theHttpRequestValidatesBoundedFactsButAcceptsOlderClients() {
    try (var factory = Validation.buildDefaultValidatorFactory()) {
      var validator = factory.getValidator();
      var invalid = new NpcTravelContext(NpcTravelContext.Phase.GOING_HOME, -1, 10001, -1, -1);
      assertThat(
              validator.validate(
                  new StartConversationRequest(
                      UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER, true, invalid)))
          .hasSize(4);
      assertThat(
              validator.validate(
                  new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER)))
          .isEmpty();
    }
  }
}
