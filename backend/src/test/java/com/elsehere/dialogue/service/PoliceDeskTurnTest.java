package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.argThat;
import static org.mockito.ArgumentMatchers.contains;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.MissionDialogueService;
import com.elsehere.mission.service.TownStoryService;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.NullSource;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.test.util.ReflectionTestUtils;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

// 경찰서 창구에서 신고를 받는 턴. 판정·대답은 Mock으로 정하고 DB 없이 턴 흐름만 본다.
class PoliceDeskTurnTest {

  private static final String REPORT = "A man is shouting at people near the bakery.";

  private final AiGateway worker = mock(AiGateway.class);
  private final TurnRepository turns = mock(TurnRepository.class);
  private final DialogueContexts contexts = mock(DialogueContexts.class);
  private Conversation conversation;
  private TurnService service;

  @BeforeEach
  void setUp() {
    when(turns.findByRequestId(any())).thenReturn(Optional.empty());
    when(turns.save(any())).thenAnswer(call -> call.getArgument(0));
    conversation = Conversation.start(8L, 6L, InitiatedBy.USER, 1, Instant.EPOCH);
    ConversationLookup lookup = mock(ConversationLookup.class);
    when(lookup.ownedConversation(8L, 10L)).thenReturn(conversation);
    when(lookup.ownedConversationForUpdate(8L, 10L)).thenReturn(conversation);
    WorkerNpc officer =
        new WorkerNpc("POLICE_OFFICER", "Dan", "officer", "", "police station", "voice", 1);
    when(contexts.npcOf(any()))
        .thenReturn(
            new NpcContext(
                6L,
                new NpcSummaryResponse("POLICE_OFFICER", "Dan", "경찰관"),
                "TOWN",
                true,
                officer,
                "voice",
                null,
                null));
    when(contexts.history(10L)).thenReturn(List.of());
    when(worker.guard(any(), any(), any(), any()))
        .thenReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenReturn(new NpcReply("I'm on my way.", null, null, List.of()));
    TransactionTemplate transactions = mock(TransactionTemplate.class);
    when(transactions.execute(any()))
        .thenAnswer(
            call -> {
              TransactionCallback<?> callback = call.getArgument(0);
              return callback.doInTransaction(mock(TransactionStatus.class));
            });
    var noStory =
        new TownStoryService(
            mock(TownStoryRepository.class),
            mock(FirstDayMissionRepository.class),
            mock(NpcRepository.class),
            mock(com.elsehere.conversation.repository.ConversationRepository.class));
    service =
        new TurnService(
            turns,
            lookup,
            contexts,
            mock(ConversationResponses.class),
            mock(NpcAudioReceiver.class),
            worker,
            mock(MemoryService.class),
            mock(com.elsehere.review.service.ReviewCardService.class),
            mock(ReviewDialogue.class),
            mock(NpcAffinityService.class),
            transactions,
            new MissionDialogueService(mock(FirstDayMissionService.class), noStory),
            Clock.fixed(Instant.EPOCH, ZoneOffset.UTC),
            new DialogueRequestLocks(),
            mock(com.elsehere.mission.service.PlacePracticeService.class));
  }

  @Test
  @DisplayName("경찰관에게 한 말은 신고·장소를 고르는 경찰 창구 상태로 판정한다")
  void send_judgesWithThePoliceDeskState() {
    judges("chat", "none");

    send();

    verify(worker).judge(any(), any(), argThat(state -> state == PoliceDesk.STATE), any(), any());
  }

  @Test
  @DisplayName("신고와 장소가 나오면 출동할 장소를 응답에 싣고, 경찰관이 직접 간다고 말하며 대화를 마친다")
  void send_dispatchesAndEndsTheConversation() {
    judges("report_incident", "Maple Bakery");

    TurnResponse response = send();

    assertThat(response.policeDispatch().placeCode()).isEqualTo("BAKERY");
    assertThat(conversation.isActive()).isFalse();
    verify(worker)
        .reply(
            any(),
            any(),
            any(),
            any(),
            any(),
            contains("yourself in the patrol car"),
            anyInt(),
            any());
  }

  @Test
  @DisplayName("신고가 아닌 말에는 출동하지 않고 대화를 이어 간다")
  void send_doesNotDispatchForOtherTalk() {
    judges("chat", "none");

    TurnResponse response = send();

    assertThat(response.policeDispatch()).isNull();
    assertThat(conversation.isActive()).isTrue();
  }

  @Test
  @DisplayName("무례로 센 말은 신고로 받지 않는다")
  void send_doesNotDispatchForRudeTurns() {
    judges("report_incident", "Maple Bakery");
    when(worker.replyReacting(
            any(), any(), any(), any(), any(), any(), anyInt(), any(), any(), any()))
        .thenReturn(new NpcReply("Please be polite.", null, null, List.of()));
    when(worker.guard(any(), any(), any(), any()))
        .thenReturn(new GuardResult(new GuardResult.Guard(true, null), List.of()));

    TurnResponse response = send();

    assertThat(response.policeDispatch()).isNull();
  }

  @ParameterizedTest
  @NullSource
  @ValueSource(strings = "Maple Bakery")
  @DisplayName("첫 출동 응답을 잃고 같은 요청을 다시 보내도 출동 장소를 돌려준다")
  void send_sameDispatchRequest_replaysTheDispatch(String place) {
    judges("report_incident", place == null ? "the main street" : place);
    ReflectionTestUtils.setField(conversation, "id", 10L);
    var saved = new ArrayList<Turn>();
    when(turns.save(any()))
        .thenAnswer(
            call -> {
              Turn turn = call.getArgument(0);
              saved.add(turn);
              return turn;
            });
    when(turns.findByRequestId(any()))
        .thenAnswer(
            call ->
                saved.stream()
                    .filter(turn -> call.getArgument(0).equals(turn.getRequestId()))
                    .findFirst());
    when(turns.findByConversationIdAndSeq(any(), anyInt()))
        .thenAnswer(
            call ->
                saved.stream()
                    .filter(turn -> turn.getSeq().equals(call.getArgument(1)))
                    .findFirst());
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, REPORT, null);
    TurnResponse first = service.send(8L, 10L, request);
    assertThat(first.policeDispatch()).isNotNull();
    assertThat(service.send(8L, 10L, request).policeDispatch()).isEqualTo(first.policeDispatch());
  }

  @Test
  @DisplayName("비번 경찰관은 창구 신고를 받거나 출동하지 않는다")
  void send_offDutyOfficer_doesNotDispatch() {
    conversation.markNpcOffDuty();
    NpcContext offDuty = contexts.npcOf(conversation).offDuty();
    when(contexts.npcOf(any())).thenReturn(offDuty);
    judges("report_incident", "Maple Bakery");
    assertThat(send().policeDispatch()).isNull();
    assertThat(conversation.isActive()).isTrue();
  }

  private void judges(String intent, String place) {
    when(worker.judge(any(), any(), any(), any(), any()))
        .thenReturn(
            new JudgeResult(
                REPORT,
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent(intent, List.of(intent), Map.of("place", place), null)));
  }

  private TurnResponse send() {
    return service.send(8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, REPORT, null));
  }
}
