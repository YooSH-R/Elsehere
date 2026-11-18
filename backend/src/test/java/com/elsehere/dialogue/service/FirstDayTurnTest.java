package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

/** HTTP·DB를 대체하고 실제 대화 서비스와 미션 규칙을 함께 실행한다. */
class FirstDayTurnTest {

  private static final NpcReply SAVED_REPLY =
      new NpcReply("Your suitcase is safe. The cafe is across the street.", null, null, List.of());

  private final FirstDayMission mission = FirstDayMission.start(8L);
  private final AiGateway worker = mock(AiGateway.class);
  private final Conversation conversation =
      Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
  private TurnService service;

  @BeforeEach
  void setUp() {
    FirstDayMissionRepository missions = mock(FirstDayMissionRepository.class);
    when(missions.findById(8L)).thenReturn(Optional.of(mission));
    when(missions.findForUpdate(8L)).thenReturn(Optional.of(mission));
    FirstDayMissionService firstDay =
        new FirstDayMissionService(
            missions,
            mock(ConversationRepository.class),
            mock(UserRepository.class),
            mock(NpcRepository.class),
            mock(com.elsehere.favor.repository.FavorRepository.class));
    TurnRepository turns = mock(TurnRepository.class);
    when(turns.findByRequestId(any())).thenReturn(Optional.empty());
    when(turns.save(any())).thenAnswer(invocation -> invocation.getArgument(0));
    ConversationLookup lookup = mock(ConversationLookup.class);
    DialogueContexts contexts = mock(DialogueContexts.class);
    when(lookup.ownedConversation(8L, 10L)).thenReturn(conversation);
    when(lookup.ownedConversationForUpdate(8L, 10L)).thenReturn(conversation);
    WorkerNpc npc = new WorkerNpc("HOTEL_STAFF", "Emma", "clerk", "", "hotel", "alloy", 1);
    when(contexts.npcOf(conversation))
        .thenReturn(
            new NpcContext(
                1L,
                new NpcSummaryResponse("HOTEL_STAFF", "Emma", "직원"),
                "HOTEL",
                true,
                npc,
                "alloy",
                null,
                null));
    when(contexts.history(10L)).thenReturn(List.of());
    when(worker.judge(any(), any(), any(), any(), any()))
        .thenReturn(
            new JudgeResult(
                "Can you store my suitcase? Where is a cafe? My booking is under Kim.",
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent(
                    "confirm_reservation",
                    List.of("confirm_reservation", "store_suitcase", "ask_cafe"),
                    null)));
    TransactionTemplate transactions = mock(TransactionTemplate.class);
    when(transactions.execute(any()))
        .thenAnswer(
            invocation -> {
              TransactionCallback<?> callback = invocation.getArgument(0);
              return callback.doInTransaction(mock(TransactionStatus.class));
            });
    service =
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
            new com.elsehere.mission.service.QuestTalkService(
                firstDay,
                mock(com.elsehere.mission.service.TownStoryService.class),
                mock(com.elsehere.mission.service.PlacePracticeService.class),
                mock(com.elsehere.mission.service.ShowcaseQuestService.class)),
            Clock.fixed(Instant.EPOCH, ZoneOffset.UTC),
            new DialogueRequestLocks(),
            new EarlyReplies(true),
            mock(MeaningPreparation.class),
            NoFavors.service());
  }

  @Test
  void onlySuccessfulSavedReplyAppliesRecognizedGoals() {
    guards(false);
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE))
        .thenReturn(SAVED_REPLY);
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Hello", null);
    assertThatThrownBy(() -> service.send(8L, 10L, request)).isInstanceOf(BusinessException.class);
    assertThat(mission.getStage()).isZero();
    service.send(8L, 10L, request);
    assertThat(mission.getStage()).isEqualTo(1);
  }

  @Test
  void rudeTurnDoesNotApplyRecognizedGoals() {
    guards(true);
    when(worker.replyReacting(
            any(), any(), any(), any(), any(), any(), anyInt(), any(), any(), any()))
        .thenReturn(SAVED_REPLY);

    service.send(8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Hello", null));

    assertThat(mission.getStage()).isZero();
  }

  @Test
  void hotelTurnDuringCafeStageSendsOnlyDirectionsToWorkerAndDoesNotAdvance() {
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    guards(false);
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenReturn(SAVED_REPLY);

    service.send(
        8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Where is the cafe?", null));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    verify(worker).reply(any(), any(), any(), any(), any(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue())
        .contains("Corner Cafe", "Stay in your own role")
        .doesNotContain("Offer drinks");
    assertThat(mission.getStage()).isEqualTo(1);
    assertThat(mission.hasGoal(0)).isFalse();
  }

  @Test
  @DisplayName("이 턴으로 그 단계의 목표를 모두 마치면 NPC가 마무리 대사로 대화를 끝낸다")
  void turnThatCompletesTheStageWrapsUpTheConversation() {
    guards(false);
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenReturn(SAVED_REPLY);

    service.send(8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Hello", null));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    verify(worker).reply(any(), any(), any(), any(), any(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue()).endsWith(WrapUp.INSTRUCTION);
    assertThat(conversation.getStatus()).isEqualTo(ConversationStatus.ENDED_BY_NPC);
    assertThat(conversation.getEndReason()).isEqualTo(ConversationEndReason.NPC_ENDED);
    assertThat(mission.getStage()).isEqualTo(1);
  }

  @Test
  @DisplayName("목표가 남으면 마무리하지 않고 대화를 이어 간다")
  void turnThatLeavesGoalsOpenKeepsTheConversationGoing() {
    when(worker.judge(any(), any(), any(), any(), any()))
        .thenReturn(
            new JudgeResult(
                "My booking is under Kim.",
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent(
                    "confirm_reservation", List.of("confirm_reservation"), null)));
    guards(false);
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenReturn(SAVED_REPLY);

    service.send(8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Kim", null));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    verify(worker).reply(any(), any(), any(), any(), any(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue()).doesNotContain(WrapUp.INSTRUCTION);
    assertThat(conversation.isActive()).isTrue();
  }

  @Test
  @DisplayName("무례로 센 턴은 목표를 모두 말해도 마무리하지 않는다")
  void rudeTurnDoesNotWrapUp() {
    guards(true);
    when(worker.replyReacting(
            any(), any(), any(), any(), any(), any(), anyInt(), any(), any(), any()))
        .thenReturn(SAVED_REPLY);

    service.send(8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Hello", null));

    assertThat(conversation.isActive()).isTrue();
  }

  private void guards(boolean abusive) {
    when(worker.guard(any(), any(), any(), any()))
        .thenReturn(new GuardResult(new GuardResult.Guard(abusive, null), List.of()));
  }
}
