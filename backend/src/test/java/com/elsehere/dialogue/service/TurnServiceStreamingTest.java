package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.reply;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

// 발화 전송(CONV-03)을 스트리밍으로 받을 때. 판정·대답·조각 음성은 Mock으로 정한다.
@DialogueDbTest
class TurnServiceStreamingTest {

  private static final String SAID = "Hi there";
  private static final byte[] CHUNK_WAV = {82, 73, 70, 70, 1};
  private static final byte[] FULL_WAV = {82, 73, 70, 70, 2};
  // 먼저 시작한 대답을 판정이 기다려 보는 시간. 대답이 먼저 시작해야 하는 테스트는 넉넉히, 시작하지 않아야 하는 테스트는 짧게 본다.
  private static final long EARLY_WAIT_MS = 2000;
  private static final long LATE_WAIT_MS = 500;

  @Autowired private TurnService turnService;

  @Autowired private DialogueStartService dialogueStartService;

  @Autowired private AiGateway aiGateway;

  @Autowired private TurnRepository turnRepository;

  @Autowired private FirstDayMissionRepository firstDayMissionRepository;

  @Autowired private EntityManager entityManager;

  private final List<String> received = new ArrayList<>();
  private final TurnEvents events =
      new TurnEvents() {
        @Override
        public void delta(String text) {
          received.add("delta:" + text);
        }

        @Override
        public void audio(int seq, String text, byte[] wav) {
          received.add("audio:" + seq + ":" + text + ":" + (wav == null ? "none" : wav.length));
        }
      };

  private Long userId;
  private Long conversationId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversationId =
        dialogueStartService
            .start(
                userId,
                new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC))
            .conversationId();
    given(aiGateway.judge(any(), any(), any(), any(), eq(SAID)))
        .willReturn(new JudgeResult(SAID, false, null, null, List.of(usage("intent"))));
    given(aiGateway.guard(any(), any(), any(), eq(SAID)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of(usage("guard"))));
    given(aiGateway.audio(any(), eq("full-1"))).willReturn(FULL_WAV);
  }

  @Test
  @DisplayName("대답 글자 조각과 문장 조각 음성을 받는 대로 넘기고, 끝나면 턴을 저장해 REST와 같은 결과를 돌려준다")
  void send_withEvents_relaysDeltasAndChunkAudioThenSavesTheTurn() {
    repliesWith(
        listener -> {
          listener.onDelta("Hello!");
          listener.onChunk(new NpcReplyChunk(0, "Hello!", "chunk-0"));
          listener.onDelta(" Nice day.");
          listener.onChunk(new NpcReplyChunk(1, "Nice day.", "chunk-1"));
        });
    given(aiGateway.audio(any(), eq("chunk-0"))).willReturn(CHUNK_WAV);
    given(aiGateway.audio(any(), eq("chunk-1"))).willReturn(CHUNK_WAV);
    UUID requestId = UUID.randomUUID();

    TurnResponse response = turnService.send(userId, conversationId, request(requestId), events);

    assertThat(received)
        .containsExactly(
            "delta:Hello!", "audio:0:Hello!:5", "delta: Nice day.", "audio:1:Nice day.:5");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
    assertThat(turnRepository.findByRequestId(requestId)).isPresent();
  }

  @Test
  @DisplayName("합성이 늦거나 실패해 음성이 없는 조각은 음성 없이 넘기고 워커에 받으러 가지 않는다")
  void send_withEvents_chunkWithoutAudio_relaysTextOnly() {
    repliesWith(listener -> listener.onChunk(new NpcReplyChunk(0, "Hello!", null)));

    turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(received).containsExactly("audio:0:Hello!:none");
    then(aiGateway).should(never()).audio(any(), eq(null));
  }

  @Test
  @DisplayName("조각 음성을 받지 못하면 그 조각만 음성 없이 넘기고 대답은 끝까지 이어 간다")
  void send_withEvents_chunkDownloadFails_relaysTextOnlyAndContinues() {
    repliesWith(
        listener -> {
          listener.onChunk(new NpcReplyChunk(0, "Hello!", "chunk-0"));
          listener.onDelta(" Nice day.");
        });
    given(aiGateway.audio(any(), eq("chunk-0")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    TurnResponse response =
        turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(received).containsExactly("audio:0:Hello!:none", "delta: Nice day.");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
  }

  @Test
  @DisplayName("같은 요청 ID로 다시 보내면 조각 없이 저장된 결과만 돌려주고 AI를 다시 부르지 않는다")
  void send_withEvents_sameRequestId_replaysWithoutEvents() {
    repliesWith(listener -> listener.onDelta("Hello! Nice day."));
    UUID requestId = UUID.randomUUID();
    TurnResponse first = turnService.send(userId, conversationId, request(requestId), events);
    received.clear();

    TurnResponse again = turnService.send(userId, conversationId, request(requestId), events);

    assertThat(received).isEmpty();
    assertThat(again.npcTurn().turnId()).isEqualTo(first.npcTurn().turnId());
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any(), any());
  }

  @Test
  @DisplayName("목표 없는 대화는 판정이 끝나기 전에 대답을 먼저 시작하고, 판정이 대답을 바꾸지 않으면 쥐고 있던 조각부터 그 대답을 넘긴다")
  void send_freeTalk_startsTheReplyBeforeJudgingAndKeepsIt() {
    CountDownLatch replyStarted = new CountDownLatch(1);
    AtomicBoolean startedBeforeJudged = new AtomicBoolean();
    judgesAfter(replyStarted, EARLY_WAIT_MS, startedBeforeJudged, plainJudge(SAID));
    repliesWith(
        listener -> {
          replyStarted.countDown();
          listener.onDelta("Hello!");
          listener.onDelta(" Nice day.");
        });

    TurnResponse response =
        turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(startedBeforeJudged).isTrue();
    assertThat(received).containsExactly("judged", "delta:Hello!", "delta: Nice day.");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
    then(aiGateway)
        .should(times(1))
        .reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any(), any());
  }

  @Test
  @DisplayName("판정에서 칭찬이 나오면 먼저 시작한 대답을 버려 그 조각을 넘기지 않고, 칭찬 반응으로 다시 부른 대답을 넘긴다")
  void send_freeTalkWithCompliment_discardsTheEarlyReplyAndRepliesReacting() {
    CountDownLatch earlyPiece = new CountDownLatch(1);
    judgesAfter(
        earlyPiece,
        EARLY_WAIT_MS,
        new AtomicBoolean(),
        new JudgeResult(
            SAID, false, null, null, List.of(usage("intent")), null, List.of("COMPLIMENT"), 0));
    repliesWith(
        listener -> {
          listener.onDelta("Early.");
          earlyPiece.countDown();
        });
    given(
            aiGateway.replyReacting(
                any(),
                any(),
                any(),
                any(),
                eq(SAID),
                any(),
                anyInt(),
                any(),
                any(),
                eq("compliment")))
        .willAnswer(
            invocation -> {
              NpcReplyListener listener = invocation.getArgument(8);
              listener.onDelta("Thank you!");
              return reply("Thank you!", "full-1");
            });

    TurnResponse response =
        turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(received).containsExactly("judged", "delta:Thank you!");
    assertThat(response.npcTurn().text()).isEqualTo("Thank you!");
  }

  @Test
  @DisplayName("판정이 행동 묘사를 지워 말이 달라지면 먼저 시작한 대답을 버리고, 지운 말로 다시 부른 대답을 넘긴다")
  void send_freeTalkWithActionRemoved_discardsTheEarlyReplyAndRepliesToTheSpokenText() {
    String typed = "*waves* " + SAID;
    CountDownLatch earlyPiece = new CountDownLatch(1);
    judgesAfter(earlyPiece, EARLY_WAIT_MS, new AtomicBoolean(), plainJudge(SAID));
    given(aiGateway.guard(any(), any(), any(), eq(typed)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    given(aiGateway.reply(any(), any(), any(), any(), eq(typed), any(), anyInt(), any(), any()))
        .willAnswer(
            invocation -> {
              NpcReplyListener listener = invocation.getArgument(8);
              listener.onDelta("Early.");
              earlyPiece.countDown();
              return reply("Early.", "full-1");
            });
    repliesWith(listener -> listener.onDelta("Hello! Nice day."));

    TurnResponse response =
        turnService.send(
            userId,
            conversationId,
            new TurnRequest(UUID.randomUUID(), InputType.TEXT, typed, null),
            events);

    assertThat(received).containsExactly("judged", "delta:Hello! Nice day.");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
  }

  @Test
  @DisplayName("행동 묘사만 있어 턴이 아니면 먼저 시작한 대답을 버리고 조각 없이 오류로 끝낸다")
  void send_freeTalkActionOnly_discardsTheEarlyReplyAndThrows() {
    String typed = "*waves*";
    CountDownLatch earlyPiece = new CountDownLatch(1);
    judgesAfter(
        earlyPiece,
        EARLY_WAIT_MS,
        new AtomicBoolean(),
        new JudgeResult("", true, null, null, List.of()));
    given(aiGateway.guard(any(), any(), any(), eq(typed)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    given(aiGateway.reply(any(), any(), any(), any(), eq(typed), any(), anyInt(), any(), any()))
        .willAnswer(
            invocation -> {
              NpcReplyListener listener = invocation.getArgument(8);
              listener.onDelta("Early.");
              earlyPiece.countDown();
              return reply("Early.", "full-1");
            });
    UUID requestId = UUID.randomUUID();

    assertThatThrownBy(
            () ->
                turnService.send(
                    userId,
                    conversationId,
                    new TurnRequest(requestId, InputType.TEXT, typed, null),
                    events))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.VALIDATION_FAILED);
    assertThat(received).containsExactly("judged");
    assertThat(turnRepository.findByRequestId(requestId)).isEmpty();
  }

  @Test
  @DisplayName("택시처럼 판정 결과가 대답 지시문에 들어가는 대화는 판정이 끝난 뒤에 대답을 시작한다")
  void send_taxiRide_startsTheReplyAfterJudging() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Where are you headed?", null));
    // 한 사용자는 대화 하나만 진행하므로 택시를 탄 다른 사용자로 본다.
    Long riderId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Rider");
    Long taxiConversationId =
        dialogueStartService
            .start(
                riderId,
                new StartConversationRequest(UUID.randomUUID(), "TAXI_DRIVER", InitiatedBy.USER))
            .conversationId();
    CountDownLatch replyStarted = new CountDownLatch(1);
    AtomicBoolean startedBeforeJudged = new AtomicBoolean();
    judgesAfter(replyStarted, LATE_WAIT_MS, startedBeforeJudged, plainJudge(SAID));
    repliesWith(
        listener -> {
          replyStarted.countDown();
          received.add("replied");
        });

    turnService.send(riderId, taxiConversationId, request(UUID.randomUUID()), events);

    assertThat(startedBeforeJudged).isFalse();
    assertThat(received).startsWith("judged", "replied");
  }

  @Test
  @DisplayName("미션 중이어도 미션과 상관없는 NPC와의 자유 대화는 판정 결과가 지시문에 들어가지 않으므로 판정이 끝나기 전에 대답을 시작한다")
  void send_freeTalkWithAnotherNpcDuringMission_startsTheReplyBeforeJudging() {
    // 첫날 미션의 첫 단계는 호텔 직원과의 대화다. 지금 말하는 상대는 길의 행인이다.
    firstDayMissionRepository.saveAndFlush(FirstDayMission.start(userId));
    CountDownLatch replyStarted = new CountDownLatch(1);
    AtomicBoolean startedBeforeJudged = new AtomicBoolean();
    judgesAfter(replyStarted, EARLY_WAIT_MS, startedBeforeJudged, plainJudge(SAID));
    repliesWith(
        listener -> {
          replyStarted.countDown();
          listener.onDelta("Hello!");
        });

    TurnResponse response =
        turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(startedBeforeJudged).isTrue();
    assertThat(received).containsExactly("judged", "delta:Hello!");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
  }

  @Test
  @DisplayName("미션 대상 NPC와의 대화는 판정이 알아본 목표가 지시문에 들어가므로 판정이 끝난 뒤에 대답을 시작한다")
  void send_missionNpc_startsTheReplyAfterJudging() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome to the hotel!", null));
    // 한 사용자는 대화 하나만 진행하므로 첫날 미션을 막 시작한 다른 사용자로 본다.
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-3", "Guest");
    firstDayMissionRepository.saveAndFlush(FirstDayMission.start(guestId));
    Long hotelConversationId =
        dialogueStartService
            .start(
                guestId,
                new StartConversationRequest(UUID.randomUUID(), "HOTEL_STAFF", InitiatedBy.USER))
            .conversationId();
    CountDownLatch replyStarted = new CountDownLatch(1);
    AtomicBoolean startedBeforeJudged = new AtomicBoolean();
    judgesAfter(replyStarted, LATE_WAIT_MS, startedBeforeJudged, plainJudge(SAID));
    repliesWith(
        listener -> {
          replyStarted.countDown();
          received.add("replied");
        });

    turnService.send(guestId, hotelConversationId, request(UUID.randomUUID()), events);

    assertThat(startedBeforeJudged).isFalse();
    assertThat(received).startsWith("judged", "replied");
  }

  @Test
  @DisplayName("조각 없이 한 번에 받는 요청은 판정이 끝난 뒤에 대답을 시작한다")
  void send_withoutEvents_startsTheReplyAfterJudging() {
    CountDownLatch replyStarted = new CountDownLatch(1);
    AtomicBoolean startedBeforeJudged = new AtomicBoolean();
    judgesAfter(replyStarted, LATE_WAIT_MS, startedBeforeJudged, plainJudge(SAID));
    given(aiGateway.reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              replyStarted.countDown();
              received.add("replied");
              return reply("Hello! Nice day.", "full-1");
            });

    turnService.send(userId, conversationId, request(UUID.randomUUID()));

    assertThat(startedBeforeJudged).isFalse();
    assertThat(received).containsExactly("judged", "replied");
  }

  private TurnRequest request(UUID requestId) {
    return new TurnRequest(requestId, InputType.TEXT, SAID, null);
  }

  private static JudgeResult plainJudge(String text) {
    return new JudgeResult(text, false, null, null, List.of(usage("intent")));
  }

  // 판정은 waitFor가 풀리기를 최대 waitMillis만큼 기다린 뒤 끝난다. 그 사이에 풀렸는지 arrived에 남긴다.
  private void judgesAfter(
      CountDownLatch waitFor, long waitMillis, AtomicBoolean arrived, JudgeResult result) {
    given(aiGateway.judge(any(), any(), any(), any(), any()))
        .willAnswer(
            invocation -> {
              arrived.set(waitFor.await(waitMillis, TimeUnit.MILLISECONDS));
              received.add("judged");
              return result;
            });
  }

  private void repliesWith(java.util.function.Consumer<NpcReplyListener> stream) {
    given(aiGateway.reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any(), any()))
        .willAnswer(
            invocation -> {
              stream.accept(invocation.getArgument(8));
              return reply("Hello! Nice day.", "full-1");
            });
  }
}
