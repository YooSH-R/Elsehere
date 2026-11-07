package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.memory.service.NpcAffinityService;
import java.time.Clock;
import java.util.List;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.json.JsonMapper;

class ConversationResponsesMeaningTest {
  @Test
  void historyIncludesSavedNpcGlossaryWithoutGeneratingItAgain() {
    TurnRepository turns = mock(TurnRepository.class);
    DialogueContexts contexts = mock(DialogueContexts.class);
    Conversation conversation = mock(Conversation.class);
    NpcContext npc = mock(NpcContext.class);
    Turn turn = mock(Turn.class);
    when(conversation.getId()).thenReturn(31L);
    when(conversation.getNpcId()).thenReturn(2L);
    when(contexts.npcById(2L)).thenReturn(npc);
    when(turns.findAllByConversationIdOrderBySeqAsc(31L)).thenReturn(List.of(turn));
    when(turn.getId()).thenReturn(41L);
    when(turn.getSpeaker()).thenReturn(Speaker.NPC);
    when(turn.getText()).thenReturn("For here or to go?");
    when(turn.getMeaningKo()).thenReturn("매장에서 드실 건가요, 포장인가요?");
    when(turn.getGlossary()).thenReturn("[{\"text\":\"to go\",\"meaningKo\":\"포장\"}]");
    JsonMapper json = JsonMapper.builder().build();
    var responses =
        new ConversationResponses(
            turns, contexts, mock(NpcAffinityService.class), json, Clock.systemUTC());

    var saved = json.valueToTree(responses.detail(conversation)).get("turns").get(0);

    assertThat(saved.get("meaningKo").asText()).isEqualTo("매장에서 드실 건가요, 포장인가요?");
    assertThat(saved.has("glossary")).isTrue();
    assertThat(saved.get("glossary").get(0).get("text").asText()).isEqualTo("to go");
  }
}
