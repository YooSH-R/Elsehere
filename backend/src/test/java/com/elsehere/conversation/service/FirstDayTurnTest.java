package com.elsehere.conversation.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.dto.TurnRequest;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.user.repository.UserRepository;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

/** HTTP·DB를 대체하고 실제 대화 서비스와 미션 규칙을 함께 실행한다. */
class FirstDayTurnTest {
  @Test
  void onlySuccessfulSavedReplyAppliesRecognizedGoals() {
    FirstDayMission mission = FirstDayMission.start(8L);
    FirstDayMissionRepository missions = mock(FirstDayMissionRepository.class);
    when(missions.findById(8L)).thenReturn(Optional.of(mission));
    when(missions.findForUpdate(8L)).thenReturn(Optional.of(mission));
    FirstDayMissionService firstDay =
        new FirstDayMissionService(
            missions, mock(ConversationRepository.class), mock(UserRepository.class));
    TurnRepository turns = mock(TurnRepository.class);
    when(turns.findByRequestId(any())).thenReturn(Optional.empty());
    when(turns.save(any())).thenAnswer(invocation -> invocation.getArgument(0));
    ConversationSupport support = mock(ConversationSupport.class);
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    when(support.ownedConversation(8L, 10L)).thenReturn(conversation);
    WorkerNpc npc = new WorkerNpc("HOTEL_STAFF", "Emma", "clerk", "", "hotel", "alloy", 1);
    when(support.npcById(1L))
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
    when(support.history(10L)).thenReturn(List.of());
    AiWorkerClient worker = mock(AiWorkerClient.class);
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
    when(worker.reply(
            any(), any(), any(), any(), any(), any(), org.mockito.ArgumentMatchers.anyInt()))
        .thenReturn(new NpcReply(null, null, null, "timeout", List.of()))
        .thenReturn(
            new NpcReply(
                "Your suitcase is safe. The cafe is across the street.",
                null,
                List.of(),
                null,
                List.of()));
    TransactionTemplate transactions = mock(TransactionTemplate.class);
    when(transactions.execute(any()))
        .thenAnswer(
            invocation -> {
              TransactionCallback<?> callback = invocation.getArgument(0);
              return callback.doInTransaction(mock(TransactionStatus.class));
            });
    TurnService service =
        new TurnService(
            turns,
            support,
            worker,
            mock(AiRequestRecorder.class),
            mock(MemoryService.class),
            transactions,
            firstDay);
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Hello", null);
    assertThatThrownBy(() -> service.send(8L, 10L, request)).isInstanceOf(BusinessException.class);
    assertThat(mission.getStage()).isZero();
    service.send(8L, 10L, request);
    assertThat(mission.getStage()).isEqualTo(1);
  }
}
