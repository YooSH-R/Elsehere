package com.elsehere.dialogue.service;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.WorkerSpeechPhrase;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.conversation.service.NpcAudioRetention;
import java.time.Clock;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.Spy;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;
import tools.jackson.databind.json.JsonMapper;

@ExtendWith(MockitoExtension.class)
class SpeechEmotionRegenerationTest {
  @Mock private ConversationLookup lookup;
  @Mock private DialogueContexts contexts;
  @Mock private AiGateway worker;
  @Mock private NpcAudioRetention retention;
  @Mock private TransactionTemplate transactions;
  @Spy private JsonMapper jsonMapper = JsonMapper.builder().build();
  @Spy private Clock clock = Clock.systemUTC();
  @InjectMocks private SpeechService service;

  @Test
  void regeneratedSpeechUsesStoredEmotionsInsteadOfDiscardingThem() {
    Conversation conversation = mock(Conversation.class);
    when(conversation.getNpcId()).thenReturn(4L);
    when(conversation.getId()).thenReturn(10L);
    NpcContext context = mock(NpcContext.class);
    when(context.voiceId()).thenReturn("af_heart");
    when(contexts.npcById(4L)).thenReturn(context);
    Turn turn = Turn.npc(conversation, 1, "Are you okay? Let me help.", null, null);
    turn.attachSpeechPhrases(
        """
        [{"text":"Are you okay?","emotion":"concerned","intensity":0.7},
         {"text":"Let me help.","emotion":"warm_smile","intensity":0.6}]
        """);
    when(lookup.ownedNpcTurn(2L, 9L)).thenReturn(turn);
    when(retention.load(turn)).thenReturn(Optional.empty());
    when(transactions.execute(any()))
        .thenAnswer(
            invocation -> {
              TransactionCallback<?> callback = invocation.getArgument(0);
              return callback.doInTransaction(mock(TransactionStatus.class));
            });

    service.regenerateSpeech(2L, 9L, UUID.randomUUID(), 1.1);

    verify(worker)
        .synthesize(
            any(),
            eq(turn.getText()),
            eq("af_heart"),
            eq(1.1),
            eq(
                List.of(
                    new WorkerSpeechPhrase("Are you okay?", "concerned", 0.7),
                    new WorkerSpeechPhrase("Let me help.", "warm_smile", 0.6))));
  }
}
