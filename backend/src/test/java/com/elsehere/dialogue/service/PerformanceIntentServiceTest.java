package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.PerformanceIntentResult;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

class PerformanceIntentServiceTest {
  private final ConversationLookup conversations = mock(ConversationLookup.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final AiGateway gateway = mock(AiGateway.class);
  private final TransactionTemplate transaction = mock(TransactionTemplate.class);
  private final Conversation conversation = mock(Conversation.class);
  private final Npc npc = mock(Npc.class);
  private final PerformanceIntentService service =
      new PerformanceIntentService(conversations, npcs, gateway, transaction);

  @BeforeEach
  void setUp() {
    when(transaction.execute(any()))
        .thenAnswer(call -> ((TransactionCallback<?>) call.getArgument(0)).doInTransaction(null));
    when(conversations.ownedConversation(7L, 70L)).thenReturn(conversation);
    when(conversation.getNpcId()).thenReturn(37L);
    when(conversation.isActive()).thenReturn(true);
    when(npcs.findById(37L)).thenReturn(Optional.of(npc));
    when(npc.getCode()).thenReturn("TOWN_JOHN_PARK");
  }

  @Test
  void replayIntentCallsOnlyClassifier() {
    when(gateway.performanceIntent(any(), any()))
        .thenReturn(new PerformanceIntentResult("REPLAY_RAP", List.of()));
    assertThat(service.classify(7L, 70L, UUID.randomUUID(), "An encore of that beat?"))
        .isEqualTo("REPLAY_RAP");
    verify(gateway).performanceIntent(any(), any());
    verifyNoMoreInteractions(gateway);
  }

  @Test
  void otherNpcDoesNotCallModel() {
    when(npc.getCode()).thenReturn("CAFE_STAFF");
    assertThatThrownBy(() -> service.classify(7L, 70L, UUID.randomUUID(), "Again?"))
        .isInstanceOf(BusinessException.class);
    verifyNoInteractions(gateway);
  }

  @Test
  void endedConversationDoesNotCallModel() {
    when(conversation.isActive()).thenReturn(false);
    assertThatThrownBy(() -> service.classify(7L, 70L, UUID.randomUUID(), "Again?"))
        .isInstanceOf(BusinessException.class);
    verifyNoInteractions(gateway);
  }

  @Test
  void reviewDoesNotCallModel() {
    when(conversation.isReview()).thenReturn(true);
    assertThatThrownBy(() -> service.classify(7L, 70L, UUID.randomUUID(), "Again?"))
        .isInstanceOf(BusinessException.class);
    verifyNoInteractions(gateway);
  }
}
