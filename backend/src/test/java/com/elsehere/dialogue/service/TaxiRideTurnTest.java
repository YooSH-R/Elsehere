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
import com.elsehere.mission.service.QuestTalkService;
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
import org.springframework.test.util.ReflectionTestUtils;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

// 택시 기사에게 목적지를 말하는 턴. 판정·대답은 Mock으로 정하고 DB 없이 턴 흐름만 본다.
class TaxiRideTurnTest {

  private static final String REQUEST = "Go there, please.";

  private final AiGateway worker = mock(AiGateway.class);
  private final TurnRepository turns = mock(TurnRepository.class);
  private final DialogueContexts contexts = mock(DialogueContexts.class);
  private Conversation conversation;
  private TurnService service;

  @BeforeEach
  void setUp() {
    when(turns.findByRequestId(any())).thenReturn(Optional.empty());
    when(turns.save(any())).thenAnswer(call -> call.getArgument(0));
    conversation = Conversation.start(8L, 5L, InitiatedBy.USER, 1, Instant.EPOCH);
    ConversationLookup lookup = mock(ConversationLookup.class);
    when(lookup.ownedConversation(8L, 10L)).thenReturn(conversation);
    when(lookup.ownedConversationForUpdate(8L, 10L)).thenReturn(conversation);
    WorkerNpc driver = new WorkerNpc("TAXI_DRIVER", "Joe", "driver", "", "taxi", "voice", 1);
    when(contexts.npcOf(any()))
        .thenReturn(
            new NpcContext(
                5L,
                new NpcSummaryResponse("TAXI_DRIVER", "Joe", "택시 기사"),
                "TOWN",
                true,
                driver,
                "voice",
                null,
                null));
    when(contexts.history(10L)).thenReturn(List.of());
    when(worker.guard(any(), any(), any(), any()))
        .thenReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    when(worker.reply(any(), any(), any(), any(), any(), any(), anyInt(), any()))
        .thenReturn(new NpcReply("Sure, here we go.", null, null, List.of()));
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
            // 대답 뒤 뜻 보기를 미리 만들 NPC 턴 ID가 응답에 있어야 한다.
            mock(ConversationResponses.class, org.mockito.Answers.RETURNS_DEEP_STUBS),
            mock(NpcAudioReceiver.class),
            worker,
            mock(MemoryService.class),
            mock(com.elsehere.review.service.ReviewCardService.class),
            mock(ReviewDialogue.class),
            mock(NpcAffinityService.class),
            transactions,
            new QuestTalkService(
                mock(FirstDayMissionService.class),
                noStory,
                mock(com.elsehere.mission.service.PlacePracticeService.class)),
            Clock.fixed(Instant.EPOCH, ZoneOffset.UTC),
            new DialogueRequestLocks(),
            new EarlyReplies(true),
            mock(MeaningPreparation.class));
  }

  @Test
  @DisplayName("택시 기사에게 한 말은 목적지를 고르는 택시 상태로 판정한다")
  void send_judgesWithTheTaxiRideState() {
    judges("chat", "none", List.of());

    send();

    verify(worker).judge(any(), any(), argThat(state -> state == TaxiRide.STATE), any(), any());
  }

  @Test
  @DisplayName("목적지가 나오면 응답에 장소 코드를 싣고, 기사가 그곳으로 간다고 말하며 대화를 이어 간다")
  void send_choosesTheDestinationAndKeepsTalking() {
    judges("go_to", "Maple Diner", List.of());

    TurnResponse response = send();

    assertThat(response.taxiDestination().placeCode()).isEqualTo("DINER");
    assertThat(conversation.isActive()).isTrue();
    verify(worker)
        .reply(any(), any(), any(), any(), any(), contains("Maple Diner"), anyInt(), any());
  }

  @Test
  @DisplayName("목적지를 말하며 고맙다·됐다고 해도 대화를 끝내지 않는다. 도착하면 Unity가 대화를 마친다")
  void send_doesNotWrapUpWhileRiding() {
    judges("go_to", "Maple Diner", List.of("TASK_DONE", "LEAVE"));

    send();

    assertThat(conversation.isActive()).isTrue();
  }

  @Test
  @DisplayName("목적지가 아닌 말에는 출발하지 않는다")
  void send_doesNotChooseForOtherTalk() {
    judges("chat", "none", List.of());

    assertThat(send().taxiDestination()).isNull();
  }

  @Test
  @DisplayName("무례로 센 말은 목적지로 받지 않는다")
  void send_doesNotChooseForRudeTurns() {
    judges("go_to", "Maple Diner", List.of());
    when(worker.replyReacting(
            any(), any(), any(), any(), any(), any(), anyInt(), any(), any(), any()))
        .thenReturn(new NpcReply("Please be polite.", null, null, List.of()));
    when(worker.guard(any(), any(), any(), any()))
        .thenReturn(new GuardResult(new GuardResult.Guard(true, null), List.of()));

    assertThat(send().taxiDestination()).isNull();
  }

  @Test
  @DisplayName("첫 응답을 잃고 같은 요청을 다시 보내도 목적지를 돌려준다")
  void send_sameRequest_replaysTheDestination() {
    judges("go_to", "Maple Diner", List.of());
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
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, REQUEST, null);

    TurnResponse first = service.send(8L, 10L, request);

    assertThat(first.taxiDestination()).isNotNull();
    assertThat(service.send(8L, 10L, request).taxiDestination()).isEqualTo(first.taxiDestination());
  }

  @Test
  @DisplayName("쉬는 중인 택시 기사와의 대화는 목적지로 판정하지 않는다")
  void send_offDutyDriver_doesNotChoose() {
    conversation.markNpcOffDuty();
    NpcContext offDuty = contexts.npcOf(conversation).offDuty();
    when(contexts.npcOf(any())).thenReturn(offDuty);
    judges("go_to", "Maple Diner", List.of());

    assertThat(send().taxiDestination()).isNull();
    verify(worker).judge(any(), any(), argThat(state -> state != TaxiRide.STATE), any(), any());
  }

  private void judges(String intent, String place, List<String> events) {
    when(worker.judge(any(), any(), any(), any(), any()))
        .thenReturn(
            new JudgeResult(
                REQUEST,
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent(intent, List.of(intent), Map.of("destination", place), null),
                events,
                0));
  }

  private TurnResponse send() {
    return service.send(8L, 10L, new TurnRequest(UUID.randomUUID(), InputType.TEXT, REQUEST, null));
  }
}
