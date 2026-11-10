package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;

import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.memory.service.NpcAffinityService;
import java.time.Clock;
import java.util.UUID;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;
import tools.jackson.databind.json.JsonMapper;

class ConversationResponsesCorrectionTest {
  @ParameterizedTest
  @EnumSource(InputType.class)
  void englishCorrectionUsesTheExistingCardWithoutReplacingTheLearnersText(InputType inputType) {
    String original = "Yesterday I go to the cafe.";
    String corrected = "Yesterday I went to the cafe.";
    String transcript = inputType == InputType.VOICE ? original : null;
    Turn turn =
        Turn.user(
            null,
            2,
            UUID.randomUUID(),
            inputType,
            original,
            transcript,
            corrected,
            false,
            null,
            null);
    var responses =
        new ConversationResponses(
            mock(TurnRepository.class),
            mock(DialogueContexts.class),
            mock(NpcAffinityService.class),
            JsonMapper.builder().build(),
            Clock.systemUTC());

    var response = responses.userTurn(turn);

    assertThat(response.text()).isEqualTo(original);
    assertThat(response.translation().english()).isEqualTo(corrected);
    assertThat(turn.getSttRawText()).isEqualTo(transcript);
    assertThat(turn.getInputType()).isEqualTo(inputType);
  }
}
