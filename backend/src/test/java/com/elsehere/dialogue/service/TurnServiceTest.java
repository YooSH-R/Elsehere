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

import ch.qos.logback.classic.Level;
import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogCapture;
import com.elsehere.conversation.DialogueTestData;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.SpeechStatus;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnDetailResponse;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;

// 발화 전송(CONV-03). 대화는 AI를 부르지 않는 행인 말걸기로 시작하고, 판정·대답은 Mock으로 정한다.
@DialogueServiceTest
class TurnServiceTest {

  private static final String OPENING = "Hi there! Nice weather today, isn't it?";
  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private TurnService turnService;

  @Autowired private ConversationService conversationService;

  @Autowired private SpeechService speechService;

  @Autowired private AiGateway aiGateway;

  @Autowired private TurnRepository turnRepository;

  @Autowired private EntityManager entityManager;

  private Long userId;
  private Long conversationId;
  private UUID lastRequestId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversationId = startPasserby(userId).conversationId();
  }

  @Test
  @DisplayName("NPC 대답에 대화하는 사용자의 닉네임과 캐릭터를 실어 NPC가 캐릭터에 맞는 호칭을 쓰게 한다")
  void send_passesTheLearnerToTheNpcReply() {
    judges("네 맑아요", "네 맑아요", "Yes, it's sunny.");
    given(aiGateway.reply(any(), any(), any(), any(), eq("네 맑아요"), eq(""), anyInt(), any()))
        .willReturn(reply("Great day for a walk!", "audio-2"));
    given(aiGateway.audio(any(), eq("audio-2"))).willReturn(WAV);

    turnService.send(
        userId, conversationId, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "네 맑아요", null));

    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            any(),
            any(),
            eq("네 맑아요"),
            anyString(),
            anyInt(),
            eq(new WorkerLearner("Sun", "F")));
  }

  @Test
  @DisplayName(
      "판정한 문장·번역·무례 판정값으로 사용자 턴을, NPC 대답과 음성으로 NPC 턴을 이어서 저장한다. 세 워커 요청에는 같은 요청 ID와 대화를 붙인다. 추천 단어는 따로 받으므로 비워 둔다")
  void send_savesUserAndNpcTurns() {
    judges("네 맑아요", "네 맑아요", "Yes, it's sunny.");
    given(aiGateway.reply(any(), any(), any(), any(), eq("네 맑아요"), eq(""), anyInt(), any()))
        .willReturn(reply("Great day for a walk!", "audio-2"));
    given(aiGateway.audio(any(), eq("audio-2"))).willReturn(WAV);
    UUID requestId = UUID.randomUUID();

    TurnResponse response =
        turnService.send(
            userId, conversationId, new TurnRequest(requestId, InputType.TEXT, "네 맑아요", null));

    assertThat(response.userTurn().seq()).isEqualTo(2);
    assertThat(response.userTurn().text()).isEqualTo("네 맑아요");
    assertThat(response.userTurn().rude()).isFalse();
    assertThat(response.userTurn().translation().english()).isEqualTo("Yes, it's sunny.");
    assertThat(response.npcTurn().seq()).isEqualTo(3);
    assertThat(response.npcTurn().text()).isEqualTo("Great day for a walk!");
    assertThat(response.npcTurn().suggestWords()).isNull();
    assertThat(response.npcTurn().speech().status()).isEqualTo(SpeechStatus.READY);
    assertThat(speechService.npcSpeech(userId, response.npcTurn().turnId())).isEqualTo(WAV);
    assertThat(response.conversation().status()).isEqualTo(ConversationStatus.ACTIVE);
    assertThat(response.conversation().rudeLimit()).isEqualTo(Conversation.RUDE_LIMIT);
    Turn saved = turnRepository.findByRequestId(requestId).orElseThrow();
    assertThat(saved.getTranslationEn()).isEqualTo("Yes, it's sunny.");
    assertThat(saved.getInputType()).isEqualTo(InputType.TEXT);
    assertThat(saved.getGuardAbusive()).isFalse();
    assertThat(saved.getGuardErrorCode()).isNull();
    WorkerTrace trace = new WorkerTrace(requestId, userId, conversationId);
    then(aiGateway).should().judge(eq(trace), any(), any(), any(), eq("네 맑아요"));
    then(aiGateway).should().guard(eq(trace), any(), any(), eq("네 맑아요"));
    then(aiGateway)
        .should()
        .reply(eq(trace), any(), any(), any(), eq("네 맑아요"), eq(""), anyInt(), any());
  }

  @Test
  @DisplayName("무례한 말은 횟수를 세고 경고 지시문으로 대답하며 번역 카드를 담지 않는다")
  void send_abusive_countsAndWarns() {
    judges("닥쳐 멍청아", "닥쳐 멍청아", "Shut up, idiot.");
    guards("닥쳐 멍청아", true, null);
    given(aiGateway.reply(any(), any(), any(), any(), eq("닥쳐 멍청아"), anyString(), anyInt(), any()))
        .willReturn(reply("That's not okay to say.", null));

    TurnResponse response = send("닥쳐 멍청아");

    assertThat(response.userTurn().rude()).isTrue();
    assertThat(response.userTurn().translation()).isNull();
    assertThat(response.conversation().rudeCount()).isEqualTo(1);
    assertThat(response.conversation().status()).isEqualTo(ConversationStatus.ACTIVE);
    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            any(),
            any(),
            eq("닥쳐 멍청아"),
            eq(TurnService.RUDE_WARNING),
            anyInt(),
            any());
    TurnDetailResponse saved = conversationService.detail(userId, conversationId).turns().get(1);
    assertThat(saved.rude()).isTrue();
    assertThat(saved.translation()).isNull();
  }

  @Test
  @DisplayName("무례한 말이 한도에 닿으면 퇴장 지시문으로 대답하고 NPC가 대화를 끝낸다")
  void send_abusiveAtLimit_endsConversation() {
    for (int count = 1; count <= Conversation.RUDE_LIMIT; count++) {
      String text = "you idiot " + count;
      judges(text, text, null);
      guards(text, true, null);
      given(aiGateway.reply(any(), any(), any(), any(), eq(text), anyString(), anyInt(), any()))
          .willReturn(reply("Stop it.", null));
    }

    send("you idiot 1");
    send("you idiot 2");
    TurnResponse last = send("you idiot 3");

    assertThat(last.conversation().rudeCount()).isEqualTo(Conversation.RUDE_LIMIT);
    assertThat(last.conversation().status()).isEqualTo(ConversationStatus.ENDED_BY_NPC);
    assertThat(last.conversation().endReason()).isEqualTo(ConversationEndReason.RUDE);
    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            any(),
            any(),
            eq("you idiot 3"),
            eq(TurnService.RUDE_LEAVE),
            anyInt(),
            any());
    assertThat(conversationService.detail(userId, conversationId).endedAt()).isNotNull();
  }

  @Test
  @DisplayName("무례가 쌓여 NPC가 대화를 끝내면 종료 상태와 이유를 INFO로 한 번 남기고 무례한 말은 남기지 않는다")
  void send_abusiveAtLimit_logsConversationEnded() {
    for (int count = 1; count <= Conversation.RUDE_LIMIT; count++) {
      String text = "you idiot " + count;
      judges(text, text, null);
      guards(text, true, null);
      given(aiGateway.reply(any(), any(), any(), any(), eq(text), anyString(), anyInt(), any()))
          .willReturn(reply("Stop it.", null));
    }

    try (LogCapture logs = LogCapture.atInfo(TurnService.class)) {
      send("you idiot 1");
      send("you idiot 2");
      send("you idiot 3");

      assertThat(logs.messages(Level.INFO))
          .containsExactly(
              "conversation_ended conversation_id="
                  + conversationId
                  + " status=ENDED_BY_NPC end_reason=RUDE");
    }
  }

  @Test
  @DisplayName("무례 판정을 하지 못한 말은 무례로 세지 않고 이유를 남기며 번역 카드를 담지 않는다")
  void send_guardUndecided_keepsCountAndHidesTranslation() {
    judges("네 맑아요", "네 맑아요", "Yes, it's sunny.");
    guards("네 맑아요", null, "timeout");
    repliesTo("네 맑아요", reply("Great!", null));

    TurnResponse response = send("네 맑아요");

    assertThat(response.userTurn().rude()).isFalse();
    assertThat(response.userTurn().translation()).isNull();
    assertThat(response.conversation().rudeCount()).isZero();
    Turn saved = turnRepository.findByRequestId(lastRequestId).orElseThrow();
    assertThat(saved.getGuardAbusive()).isNull();
    assertThat(saved.getGuardErrorCode()).isEqualTo("timeout");
  }

  @Test
  @DisplayName("의도 판정을 부르다 워커에 닿지 못하면 오류를 그대로 알린다")
  void send_judgeCallFails_throws() {
    given(aiGateway.judge(any(), any(), any(), any(), eq("Yes")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    assertThatThrownBy(() -> send("Yes")).isInstanceOf(BusinessException.class);
  }

  @Test
  @DisplayName("무례 판정 호출이 실패해도 판정 못 함으로 기록하고 대화를 이어 간다")
  void send_guardCallFails_continuesUndecided() {
    judges("Yes", "Yes", null);
    given(aiGateway.guard(any(), any(), any(), eq("Yes")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    repliesTo("Yes", reply("Nice!", null));

    TurnResponse response = send("Yes");

    assertThat(response.userTurn().rude()).isFalse();
    Turn saved = turnRepository.findByRequestId(lastRequestId).orElseThrow();
    assertThat(saved.getGuardAbusive()).isNull();
    assertThat(saved.getGuardErrorCode()).isEqualTo(TurnService.GUARD_WORKER_UNAVAILABLE);
  }

  @Test
  @DisplayName("다음 판정과 대답에 보내는 앞선 대화에는 무례로 센 줄만 무례 표시를 붙인다")
  void send_afterAbusive_marksRudeLineInHistory() {
    judges("you idiot", "you idiot", null);
    guards("you idiot", true, null);
    given(
            aiGateway.reply(
                any(), any(), any(), any(), eq("you idiot"), anyString(), anyInt(), any()))
        .willReturn(reply("Not okay.", null));
    send("you idiot");
    judges("Sorry", "Sorry", null);
    repliesTo("Sorry", reply("It's fine.", null));

    send("Sorry");

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiGateway).should().guard(any(), any(), history.capture(), eq("Sorry"));
    assertThat(history.getValue())
        .containsExactly(
            WorkerLine.npc(OPENING),
            new WorkerLine("me", "you idiot", null, true),
            WorkerLine.npc("Not okay."));
  }

  @Test
  @DisplayName("음성 입력은 입력 종류와 전사 원문을 함께 저장한다")
  void send_voice_savesSttRawText() {
    judges("Yes it is", "Yes it is", null);
    repliesTo("Yes it is", reply("Indeed!", null));
    UUID requestId = UUID.randomUUID();

    turnService.send(
        userId,
        conversationId,
        new TurnRequest(requestId, InputType.VOICE, "Yes it is", "yes it is"));

    Turn saved = turnRepository.findByRequestId(requestId).orElseThrow();
    assertThat(saved.getInputType()).isEqualTo(InputType.VOICE);
    assertThat(saved.getSttRawText()).isEqualTo("yes it is");
  }

  @Test
  @DisplayName("판정과 대답에는 앞선 대화를 함께 보낸다")
  void send_sendsHistory() {
    judges("Yes", "Yes", null);
    repliesTo("Yes", reply("Nice!", null));

    turnService.send(
        userId, conversationId, new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Yes", null));

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiGateway).should().judge(any(), any(), any(), history.capture(), eq("Yes"));
    assertThat(history.getValue()).containsExactly(WorkerLine.npc(OPENING));
  }

  @Test
  @DisplayName("의도 판정이 고른 대화 사건을 학습자 턴에 저장하고 다음 판정·대답의 history로 보낸다")
  void send_savesJudgeEventsAndSendsThemInHistory() {
    given(aiGateway.judge(any(), any(), any(), any(), eq("Hi!")))
        .willReturn(
            new JudgeResult(
                "Hi!",
                false,
                null,
                null,
                List.of(usage("intent")),
                null,
                List.of("GREETING"),
                null));
    guards("Hi!", false, null);
    repliesTo("Hi!", reply("Hello!", null));
    judges("Bye", "Bye", null);
    repliesTo("Bye", reply("See you!", null));

    send("Hi!");
    send("Bye");

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), history.capture(), eq("Bye"), anyString(), anyInt(), any());
    assertThat(history.getValue())
        .containsExactly(
            WorkerLine.npc(OPENING),
            WorkerLine.learner("Hi!", null, false, List.of("GREETING")),
            WorkerLine.npc("Hello!"));
  }

  @Test
  @DisplayName("같은 요청 ID로 다시 보내면 턴을 새로 만들지 않고 저장된 결과를 돌려준다")
  void send_sameRequestIdTwice_replaysSavedTurns() {
    judges("Yes", "Yes", null);
    repliesTo("Yes", reply("Nice!", null));
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Yes", null);
    TurnResponse first = turnService.send(userId, conversationId, request);

    TurnResponse replayed = turnService.send(userId, conversationId, request);

    assertThat(replayed).isEqualTo(first);
    assertThat(conversationService.detail(userId, conversationId).turns()).hasSize(3);
    then(aiGateway).should().judge(any(), any(), any(), any(), anyString());
  }

  @Test
  @DisplayName("처리하는 사이 같은 요청 ID로 다시 보낸 쪽이 먼저 저장하면 새로 저장하지 않고 그 결과를 돌려준다")
  void send_sameRequestIdSavedWhileWaiting_replaysSavedTurns() {
    judges("Yes", "Yes", null);
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Yes", null);
    // 클라이언트가 시간 초과로 다시 보낸 요청이 NPC 대답을 기다리는 사이 먼저 끝나 턴을 저장한다.
    AtomicBoolean retriedOnce = new AtomicBoolean();
    AtomicReference<TurnResponse> retried = new AtomicReference<>();
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq("Yes"), eq(""), anyInt(), any()))
        .willAnswer(
            invocation -> {
              if (retriedOnce.compareAndSet(false, true)) {
                retried.set(turnService.send(userId, conversationId, request));
              }
              return reply("Nice!", null);
            });

    TurnResponse first = turnService.send(userId, conversationId, request);

    assertThat(first).isEqualTo(retried.get());
    assertThat(conversationService.detail(userId, conversationId).turns()).hasSize(3);
  }

  @Test
  @DisplayName("다른 대화에서 쓴 요청 ID로 보내면 REQUEST_ID_CONFLICT다")
  void send_requestIdUsedInOtherConversation_throwsConflict() {
    judges("Yes", "Yes", null);
    repliesTo("Yes", reply("Nice!", null));
    UUID requestId = UUID.randomUUID();
    turnService.send(
        userId, conversationId, new TurnRequest(requestId, InputType.TEXT, "Yes", null));
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");
    Long otherConversationId = startPasserby(otherUserId).conversationId();

    assertThatThrownBy(
            () ->
                turnService.send(
                    otherUserId,
                    otherConversationId,
                    new TurnRequest(requestId, InputType.TEXT, "Yes", null)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.REQUEST_ID_CONFLICT));
  }

  @Test
  @DisplayName("행동 묘사만 있는 말은 턴으로 세지 않고 안내 문구와 함께 VALIDATION_FAILED로 거절한다")
  void send_droppedText_throwsWithNotice() {
    given(aiGateway.judge(any(), any(), any(), any(), eq("*waves*")))
        .willReturn(new JudgeResult("", true, "말로 표현해 주세요.", null, List.of(usage("intent"))));

    assertThatThrownBy(
            () ->
                turnService.send(
                    userId,
                    conversationId,
                    new TurnRequest(UUID.randomUUID(), InputType.TEXT, "*waves*", null)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> {
              assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED);
              assertThat(exception.getDetails()).containsEntry("notice", "말로 표현해 주세요.");
            });
    assertThat(conversationService.detail(userId, conversationId).turns()).hasSize(1);
    then(aiGateway)
        .should(never())
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any());
  }

  @Test
  @DisplayName("NPC 대답을 만들지 못하면 그 오류로 실패하고 턴을 저장하지 않는다")
  void send_replyFails_throwsWithoutTurns() {
    judges("Yes", "Yes", null);
    given(aiGateway.reply(any(), any(), any(), any(), eq("Yes"), eq(""), anyInt(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    UUID requestId = UUID.randomUUID();

    assertThatThrownBy(
            () ->
                turnService.send(
                    userId,
                    conversationId,
                    new TurnRequest(requestId, InputType.TEXT, "Yes", null)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    assertThat(conversationService.detail(userId, conversationId).turns()).hasSize(1);
  }

  @Test
  @DisplayName("끝난 대화에는 보낼 수 없고 AI를 부르지 않는다")
  void send_endedConversation_throwsNotActive() {
    conversationService.end(
        userId, conversationId, new EndConversationRequest(UUID.randomUUID(), true));

    assertThatThrownBy(
            () ->
                turnService.send(
                    userId,
                    conversationId,
                    new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Yes", null)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.CONVERSATION_NOT_ACTIVE));
    then(aiGateway).should(never()).judge(any(), any(), any(), any(), anyString());
  }

  @Test
  @DisplayName("AI 대답을 기다리는 사이 대화가 끝났으면 턴을 저장하지 않고 CONVERSATION_NOT_ACTIVE다")
  void send_conversationEndedWhileWaiting_throwsNotActive() {
    judges("Yes", "Yes", null);
    given(aiGateway.reply(any(), any(), any(), any(), eq("Yes"), eq(""), anyInt(), any()))
        .willAnswer(
            invocation -> {
              entityManager
                  .createNativeQuery(
                      "update conversations set status = 'ENDED_BY_USER' where id = ?")
                  .setParameter(1, conversationId)
                  .executeUpdate();
              entityManager.clear();
              return reply("Nice!", null);
            });

    assertThatThrownBy(
            () ->
                turnService.send(
                    userId,
                    conversationId,
                    new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Yes", null)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.CONVERSATION_NOT_ACTIVE));
    assertThat(conversationService.detail(userId, conversationId).turns()).hasSize(1);
  }

  @Test
  @DisplayName("무례로 센 말은 워커 감점량이 없으면 기본값(-6, 공통 규칙 rudeAffinityFallback)만큼 그 NPC의 호감도를 깎는다")
  void send_abusive_deductsDefaultAffinity() {
    rudeTo("you idiot", null);

    send("you idiot");

    assertThat(affinity()).isEqualTo(-6);
  }

  @Test
  @DisplayName("무례로 깎은 뒤의 호감도를 대화 상태로 돌려준다")
  void send_abusive_respondsDeductedAffinity() {
    rudeTo("you idiot", -8);

    TurnResponse response = send("you idiot");

    assertThat(response.conversation().affinity()).isEqualTo(-8);
  }

  @Test
  @DisplayName("무례가 아닌 말에는 그 NPC의 지금 호감도를 그대로 돌려준다")
  void send_notRude_respondsCurrentAffinity() {
    entityManager
        .createNativeQuery(
            "insert into user_npc_relations (user_id, npc_id, affinity, talk_count)"
                + " values (?, ?, 12, 1)")
        .setParameter(1, userId)
        .setParameter(2, passerbyNpcId())
        .executeUpdate();
    judges("Yes", "Yes", null);
    repliesTo("Yes", reply("Nice!", null));

    TurnResponse response = send("Yes");

    assertThat(response.conversation().affinity()).isEqualTo(12);
  }

  @Test
  @DisplayName("무례 판정이 감점량을 주면 기본값 대신 그 값만큼 깎는다")
  void send_abusiveWithWorkerDelta_deductsWorkerDelta() {
    rudeTo("you idiot", -8);

    send("you idiot");

    assertThat(affinity()).isEqualTo(-8);
  }

  @Test
  @DisplayName("무례가 아니거나 무례 판정을 하지 못한 말은 호감도를 바꾸지 않는다")
  void send_notRudeOrUndecided_keepsAffinity() {
    judges("Yes", "Yes", null);
    repliesTo("Yes", reply("Nice!", null));
    judges("네 맑아요", "네 맑아요", null);
    guards("네 맑아요", null, "timeout");
    repliesTo("네 맑아요", reply("Great!", null));

    send("Yes");
    send("네 맑아요");

    assertThat(affinity()).isNull();
  }

  @Test
  @DisplayName("무례가 아닌 말은 의도 판정이 준 사건 호감 변화량만큼 바로 올리고 올린 값을 대화 상태로 돌려준다")
  void send_notRudeWithEvents_raisesAffinityRightAway() {
    judgeRaises("I love this town!", 4);
    guards("I love this town!", false, null);
    repliesTo("I love this town!", reply("Thank you!", null));

    TurnResponse response = send("I love this town!");

    assertThat(affinity()).isEqualTo(4);
    assertThat(response.conversation().affinity()).isEqualTo(4);
  }

  @Test
  @DisplayName("무례로 센 말은 의도 판정의 사건 변화량을 더하지 않고 무례 감점만 한다")
  void send_rudeWithEvents_onlyDeductsForRudeness() {
    rudeTo("you idiot, nice town though", -7);
    judgeRaises("you idiot, nice town though", 4);

    send("you idiot, nice town though");

    assertThat(affinity()).isEqualTo(-7);
  }

  @Test
  @DisplayName("무례 판정 3번으로 NPC가 대화를 끝내도 세 번 모두 깎는다")
  void send_abusiveUntilLimit_deductsEveryTime() {
    for (int count = 1; count <= Conversation.RUDE_LIMIT; count++) {
      rudeTo("you idiot " + count, null);
    }

    send("you idiot 1");
    send("you idiot 2");
    send("you idiot 3");

    assertThat(affinity()).isEqualTo(-6 * Conversation.RUDE_LIMIT);
  }

  @Test
  @DisplayName("호감도는 -100 아래로 내려가지 않는다")
  void send_abusiveNearMinimum_stopsAtMinimum() {
    entityManager
        .createNativeQuery(
            "insert into user_npc_relations (user_id, npc_id, affinity, talk_count)"
                + " values (?, ?, -98, 1)")
        .setParameter(1, userId)
        .setParameter(2, passerbyNpcId())
        .executeUpdate();
    rudeTo("you idiot", null);

    send("you idiot");

    assertThat(affinity()).isEqualTo(-100);
  }

  @Test
  @DisplayName("같은 요청 ID로 다시 보낸 무례한 말은 한 번만 깎는다")
  void send_sameAbusiveRequestTwice_deductsOnce() {
    rudeTo("you idiot", null);
    TurnRequest request = new TurnRequest(UUID.randomUUID(), InputType.TEXT, "you idiot", null);

    turnService.send(userId, conversationId, request);
    turnService.send(userId, conversationId, request);

    assertThat(affinity()).isEqualTo(-6);
  }

  private TurnResponse send(String text) {
    lastRequestId = UUID.randomUUID();
    return turnService.send(
        userId, conversationId, new TurnRequest(lastRequestId, InputType.TEXT, text, null));
  }

  private ConversationStartResponse startPasserby(Long ownerId) {
    return conversationService.start(
        ownerId, new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));
  }

  private void judges(String text, String judgedText, String english) {
    given(aiGateway.judge(any(), any(), any(), any(), eq(text)))
        .willReturn(
            new JudgeResult(
                judgedText,
                false,
                null,
                english == null ? null : new JudgeResult.Translation(english, null),
                List.of(usage("intent"))));
    guards(text, false, null);
  }

  // 칭찬 사건과 그 사건으로 바로 올릴 호감도를 주는 의도 판정. 무례 판정은 따로 정한다.
  private void judgeRaises(String text, int affinityDelta) {
    given(aiGateway.judge(any(), any(), any(), any(), eq(text)))
        .willReturn(
            new JudgeResult(
                text,
                false,
                null,
                null,
                List.of(usage("intent")),
                null,
                List.of("COMPLIMENT"),
                affinityDelta));
  }

  // 같은 문장에 다시 부르면 마지막 판정값으로 바뀐다.
  private void guards(String text, Boolean abusive, String errorCode) {
    given(aiGateway.guard(any(), any(), any(), eq(text)))
        .willReturn(
            new GuardResult(new GuardResult.Guard(abusive, errorCode), List.of(usage("guard"))));
  }

  private void rudeTo(String text, Integer affinityDelta) {
    judges(text, text, null);
    given(aiGateway.guard(any(), any(), any(), eq(text)))
        .willReturn(
            new GuardResult(
                new GuardResult.Guard(true, null), affinityDelta, List.of(usage("guard"))));
    given(aiGateway.reply(any(), any(), any(), any(), eq(text), anyString(), anyInt(), any()))
        .willReturn(reply("That's not okay to say.", null));
  }

  // 대화 상대(행인)와의 호감도. 관계 행이 없으면 null이다.
  private Integer affinity() {
    entityManager.flush();
    List<?> rows =
        entityManager
            .createNativeQuery(
                "select affinity from user_npc_relations where user_id = ? and npc_id = ?")
            .setParameter(1, userId)
            .setParameter(2, passerbyNpcId())
            .getResultList();
    return rows.isEmpty() ? null : ((Number) rows.get(0)).intValue();
  }

  private Long passerbyNpcId() {
    return DialogueTestData.npcId(entityManager, "TOWN_PASSERBY");
  }

  private void repliesTo(String text, NpcReply reply) {
    given(aiGateway.reply(any(), any(), any(), any(), eq(text), eq(""), anyInt(), any()))
        .willReturn(reply);
  }
}
