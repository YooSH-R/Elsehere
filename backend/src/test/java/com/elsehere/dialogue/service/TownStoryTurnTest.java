package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.QuestTalkService;
import com.elsehere.mission.service.TownStoryService;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

class TownStoryTurnTest {
  @Test
  void failedReplyKeepsProgressAndSavedReplyAdvancesIt() {
    TownStory story = TownStory.start(8L);
    TownStoryRepository stories = mock(TownStoryRepository.class);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));
    var firstDay = mock(FirstDayMissionService.class);
    var town =
        new TownStoryService(
            stories,
            mock(FirstDayMissionRepository.class),
            mock(NpcRepository.class),
            mock(com.elsehere.conversation.repository.ConversationRepository.class));
    var missions =
        new QuestTalkService(
            firstDay,
            town,
            mock(com.elsehere.mission.service.PlacePracticeService.class),
            mock(com.elsehere.mission.service.ShowcaseQuestService.class));
    TurnRepository turns = mock(TurnRepository.class);
    when(turns.findByRequestId(any())).thenReturn(Optional.empty());
    when(turns.save(any())).thenAnswer(call -> call.getArgument(0));
    ConversationLookup lookup = mock(ConversationLookup.class);
    when(lookup.ownedConversation(8L, 10L))
        .thenReturn(Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH));
    when(lookup.ownedConversationForUpdate(8L, 10L))
        .thenReturn(Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH));
    DialogueContexts contexts = mock(DialogueContexts.class);
    WorkerNpc npc = new WorkerNpc("DINER_STAFF", "Rosa", "server", "", "diner", "voice", 1);
    when(contexts.npcOf(any()))
        .thenReturn(
            new NpcContext(
                1L,
                new NpcSummaryResponse("DINER_STAFF", "Rosa", "직원"),
                "TOWN",
                true,
                npc,
                "voice",
                null,
                null));
    when(contexts.history(10L)).thenReturn(List.of());
    AiGateway worker = mock(AiGateway.class);
    when(worker.guard(any(), any(), any(), any()))
        .thenReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    when(worker.judge(any(), any(), any(), any(), any()))
        .thenReturn(
            new JudgeResult(
                "Breakfast, please. I spilled my drink.",
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent(null, List.of("order_breakfast", "explain_spill"), null)));
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE))
        .thenReturn(new NpcReply("Let me help.", null, null, List.of()));
    TransactionTemplate transactions = mock(TransactionTemplate.class);
    when(transactions.execute(any()))
        .thenAnswer(
            call -> {
              TransactionCallback<?> callback = call.getArgument(0);
              return callback.doInTransaction(mock(TransactionStatus.class));
            });
    TurnService service =
        new TurnService(
            turns,
            lookup,
            contexts,
            // 대답 뒤 뜻 보기를 미리 만들 NPC 턴 ID가 응답에 있어야 한다.
            mock(ConversationResponses.class, org.mockito.Answers.RETURNS_DEEP_STUBS),
            mock(NpcAudioReceiver.class),
            worker,
            mock(MemoryService.class),
            mock(com.elsehere.review.service.ReviewCardService.class),
            mock(ReviewDialogue.class),
            mock(NpcAffinityService.class),
            transactions,
            missions,
            Clock.fixed(Instant.EPOCH, ZoneOffset.UTC),
            new DialogueRequestLocks(),
            new EarlyReplies(true),
            mock(MeaningPreparation.class));
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Hello", null);

    assertThatThrownBy(() -> service.send(8L, 10L, request)).isInstanceOf(BusinessException.class);
    assertThat(story.getStage()).isZero();
    service.send(8L, 10L, request);
    assertThat(story.getStage()).isZero();
    assertThat(story.hasGoal(0)).isTrue();
    assertThat(story.hasGoal(1)).isFalse();
  }
}
