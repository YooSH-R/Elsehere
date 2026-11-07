package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.BDDMockito.given;
import static org.mockito.Mockito.mock;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.Scene;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.GameClockService;
import com.elsehere.world.service.GameTime;
import java.util.Optional;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class DialogueTimeTest {

  @ParameterizedTest
  @CsvSource({"0,00:00", "360,06:00", "720,12:00", "1140,19:00", "1439,23:59"})
  void openingAndFollowingTurnsKnowThePlayersCurrentGameTime(int minute, String time) {
    NpcRepository repository = mock(NpcRepository.class);
    WorkerNpcs workerNpcs = mock(WorkerNpcs.class);
    GameClockService clock = mock(GameClockService.class);
    Npc npc = mock(Npc.class);
    Scene scene = mock(Scene.class);
    given(npc.getId()).willReturn(21L);
    given(npc.getName()).willReturn("Ruby");
    given(npc.getScene()).willReturn(scene);
    given(repository.findByCode("TOWN_RETIREE")).willReturn(Optional.of(npc));
    given(repository.findById(21L)).willReturn(Optional.of(npc));
    given(workerNpcs.of(npc))
        .willReturn(
            new WorkerNpc(
                "TOWN_RETIREE", "Ruby", "morning walker", "", "Town street.", "voice", 1));
    given(clock.currentTime(1L)).willReturn(new GameTime(2, minute));
    given(clock.currentTime(2L)).willReturn(new GameTime(1, 480));
    DialogueContexts contexts =
        new DialogueContexts(repository, null, null, workerNpcs, null, clock);
    Conversation conversation = mock(Conversation.class);
    given(conversation.getNpcId()).willReturn(21L);
    given(conversation.getUserId()).willReturn(1L);
    given(conversation.isNpcOffDuty()).willReturn(true);

    assertThat(contexts.npcByCode(1L, "TOWN_RETIREE", false).workerNpc().scene())
        .contains("Town street.", "Current game time: day 2, " + time);
    assertThat(contexts.npcOf(conversation).workerNpc().scene())
        .contains("off work", "Current game time: day 2, " + time);
    assertThat(contexts.npcByCode(2L, "TOWN_RETIREE", false).workerNpc().scene())
        .contains("Current game time: day 1, 08:00");
  }
}
