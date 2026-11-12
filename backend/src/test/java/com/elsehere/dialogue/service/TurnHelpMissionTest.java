package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.HintResult;
import com.elsehere.ai.client.SuggestResult;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.mission.service.QuestTalk;
import com.elsehere.mission.service.QuestTalkService;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;
import tools.jackson.databind.json.JsonMapper;

class TurnHelpMissionTest {
  private final ConversationLookup lookup = mock(ConversationLookup.class);
  private final DialogueContexts contexts = mock(DialogueContexts.class);
  private final QuestTalkService missions = mock(QuestTalkService.class);
  private final AiGateway gateway = mock(AiGateway.class);
  private Conversation conversation;
  private final TurnHelpService service =
      new TurnHelpService(
          mock(TurnRepository.class),
          lookup,
          mock(com.elsehere.review.service.ReviewCardService.class),
          contexts,
          missions,
          mock(ConversationResponses.class),
          gateway,
          new DialogueRequestLocks(),
          new TransactionTemplate(mock(PlatformTransactionManager.class)),
          JsonMapper.builder().build(),
          Clock.systemUTC());

  @BeforeEach
  void prepareMissingBagConversation() {
    conversation = Conversation.start(8L, 3L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachFirstDayStage(2);
    Turn turn = Turn.npc(conversation, 3, "Where did you leave your bag?", null, Instant.EPOCH);
    when(lookup.ownedNpcTurn(8L, 10L)).thenReturn(turn);
    when(contexts.npcOf(conversation))
        .thenReturn(
            new NpcContext(
                3L,
                new NpcSummaryResponse("HOTEL_STAFF", "Jake", "호텔 직원"),
                "HOTEL",
                true,
                new WorkerNpc("HOTEL_STAFF", "Jake", "hotel clerk", "", "hotel", "", 1),
                "",
                null,
                null));
    WorkerState state =
        new WorkerState(
            "first_day_2",
            "Missing bag",
            List.of(
                new WorkerState.Intent(
                    "report_left_bag", "Explain which bag is missing and where it was left."),
                new WorkerState.Intent(
                    "return_later", "Say they will return after retrieving the bag.")));
    when(missions.forTurn(8L, "HOTEL_STAFF", conversation))
        .thenReturn(
            Optional.of(
                new QuestTalkService.SelectedQuest(
                    QuestTalkService.Source.FIRST_DAY,
                    new QuestTalk.Goals(
                        2, state, "NPC-only instructions", List.of("report_left_bag")))));
    when(gateway.hint(any(), any(), anyList(), anyString(), anyInt()))
        .thenReturn(
            new HintResult(
                List.of(
                    new HintResult.Phrase(
                        "I left my small bag at the cafe.", "카페에 작은 가방을 두고 왔어요.")),
                List.of(),
                List.of()));
    when(gateway.suggest(any(), any(), anyList(), anyString(), anyInt()))
        .thenReturn(new SuggestResult(List.of("small bag", "left at", "the cafe"), List.of()));
  }

  @Test
  void hintUsesThePendingMissionObjectiveInsteadOfAnEmptyGoal() {
    assertThat(service.hint(8L, 10L, UUID.randomUUID()).hintText()).contains("small bag");
    ArgumentCaptor<String> goal = ArgumentCaptor.forClass(String.class);
    verify(gateway).hint(any(), any(), anyList(), goal.capture(), anyInt());
    assertThat(goal.getValue())
        .contains("which bag", "where")
        .doesNotContain("return after", "NPC-only");
  }

  @Test
  void suggestedWordsUseTheSamePendingMissionGoal() {
    assertThat(service.suggestWords(8L, 10L, UUID.randomUUID()).suggestWords())
        .containsExactly(
            "left at"); // 추천 문장(I left my small bag at the cafe.)에 든 small bag·the cafe는 뺀다
    ArgumentCaptor<String> goal = ArgumentCaptor.forClass(String.class);
    verify(gateway).suggest(any(), any(), anyList(), goal.capture(), anyInt());
    assertThat(goal.getValue())
        .contains("which bag", "where")
        .doesNotContain("return after", "NPC-only");
  }

  @Test
  void anOldConversationDoesNotReceiveTheNewMissionGoal() {
    conversation.attachFirstDayStage(0);
    service.hint(8L, 10L, UUID.randomUUID());
    ArgumentCaptor<String> goal = ArgumentCaptor.forClass(String.class);
    verify(gateway).hint(any(), any(), anyList(), goal.capture(), anyInt());
    assertThat(goal.getValue()).isEmpty();
  }

  @Test
  void offDutySmallTalkDoesNotReceiveAServiceMissionGoal() {
    conversation.markNpcOffDuty();
    service.hint(8L, 10L, UUID.randomUUID());
    ArgumentCaptor<String> goal = ArgumentCaptor.forClass(String.class);
    verify(gateway).hint(any(), any(), anyList(), goal.capture(), anyInt());
    assertThat(goal.getValue()).isEmpty();
  }
}
