package com.elsehere.conversation.service;

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

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.DialogueTestData;
import com.elsehere.conversation.dto.ConversationStartResponse;
import com.elsehere.conversation.dto.EndConversationRequest;
import com.elsehere.conversation.dto.SpeechStatus;
import com.elsehere.conversation.dto.StartConversationRequest;
import com.elsehere.conversation.dto.TurnDetailResponse;
import com.elsehere.conversation.dto.TurnRequest;
import com.elsehere.conversation.dto.TurnResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.UUID;
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

  @Autowired private AiWorkerClient aiWorkerClient;

  @Autowired private TurnRepository turnRepository;

  @Autowired private AiRequestRepository aiRequestRepository;

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
  @DisplayName(
      "판정한 문장·번역·무례 판정값으로 사용자 턴을, NPC 대답과 음성으로 NPC 턴을 이어서 저장하고 호출 기록을 남긴다. 추천 단어는 따로 받으므로 비워 둔다")
  void send_savesUserAndNpcTurns() {
    judges("네 맑아요", "네 맑아요", "Yes, it's sunny.");
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq("네 맑아요"), eq(""), anyInt()))
        .willReturn(reply("Great day for a walk!", "audio-2"));
    given(aiWorkerClient.audio(any(), eq("audio-2"))).willReturn(WAV);
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
    assertThat(response.conversation().rudeLimit()).isEqualTo(ConversationSupport.RUDE_LIMIT);
    Turn saved = turnRepository.findByRequestId(requestId).orElseThrow();
    assertThat(saved.getTranslationEn()).isEqualTo("Yes, it's sunny.");
    assertThat(saved.getInputType()).isEqualTo(InputType.TEXT);
    assertThat(saved.getGuardAbusive()).isFalse();
    assertThat(saved.getGuardErrorCode()).isNull();
    assertThat(record(requestId, AiRequestKind.JUDGE)).isEqualTo(AiRequestStatus.OK);
    assertThat(record(requestId, AiRequestKind.GUARD)).isEqualTo(AiRequestStatus.OK);
    assertThat(record(requestId, AiRequestKind.NPC)).isEqualTo(AiRequestStatus.OK);
  }

  @Test
  @DisplayName("무례한 말은 횟수를 세고 경고 지시문으로 대답하며 번역 카드를 담지 않는다")
  void send_abusive_countsAndWarns() {
    judges("닥쳐 멍청아", "닥쳐 멍청아", "Shut up, idiot.");
    guards("닥쳐 멍청아", true, null);
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq("닥쳐 멍청아"), anyString(), anyInt()))
        .willReturn(reply("That's not okay to say.", null));

    TurnResponse response = send("닥쳐 멍청아");

    assertThat(response.userTurn().rude()).isTrue();
    assertThat(response.userTurn().translation()).isNull();
    assertThat(response.conversation().rudeCount()).isEqualTo(1);
    assertThat(response.conversation().status()).isEqualTo(ConversationStatus.ACTIVE);
    then(aiWorkerClient)
        .should()
        .reply(any(), any(), any(), any(), eq("닥쳐 멍청아"), eq(TurnService.RUDE_WARNING), anyInt());
    TurnDetailResponse saved = conversationService.detail(userId, conversationId).turns().get(1);
    assertThat(saved.rude()).isTrue();
    assertThat(saved.translation()).isNull();
  }

  @Test
  @DisplayName("무례한 말이 한도에 닿으면 퇴장 지시문으로 대답하고 NPC가 대화를 끝낸다")
  void send_abusiveAtLimit_endsConversation() {
    for (int count = 1; count <= ConversationSupport.RUDE_LIMIT; count++) {
      String text = "you idiot " + count;
      judges(text, text, null);
      guards(text, true, null);
      given(aiWorkerClient.reply(any(), any(), any(), any(), eq(text), anyString(), anyInt()))
          .willReturn(reply("Stop it.", null));
    }

    send("you idiot 1");
    send("you idiot 2");
    TurnResponse last = send("you idiot 3");

    assertThat(last.conversation().rudeCount()).isEqualTo(ConversationSupport.RUDE_LIMIT);
    assertThat(last.conversation().status()).isEqualTo(ConversationStatus.ENDED_BY_NPC);
    assertThat(last.conversation().endReason()).isEqualTo(ConversationEndReason.RUDE);
    then(aiWorkerClient)
        .should()
        .reply(any(), any(), any(), any(), eq("you idiot 3"), eq(TurnService.RUDE_LEAVE), anyInt());
    assertThat(conversationService.detail(userId, conversationId).endedAt()).isNotNull();
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
  @DisplayName("무례 판정 호출이 실패해도 판정 못 함으로 기록하고 대화를 이어 간다")
  void send_guardCallFails_continuesUndecided() {
    judges("Yes", "Yes", null);
    given(aiWorkerClient.guard(any(), any(), any(), eq("Yes")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    repliesTo("Yes", reply("Nice!", null));

    TurnResponse response = send("Yes");

    assertThat(response.userTurn().rude()).isFalse();
    Turn saved = turnRepository.findByRequestId(lastRequestId).orElseThrow();
    assertThat(saved.getGuardAbusive()).isNull();
    assertThat(saved.getGuardErrorCode()).isEqualTo(TurnService.GUARD_WORKER_UNAVAILABLE);
    assertThat(record(lastRequestId, AiRequestKind.GUARD)).isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("다음 판정과 대답에 보내는 앞선 대화에는 무례로 센 줄만 무례 표시를 붙인다")
  void send_afterAbusive_marksRudeLineInHistory() {
    judges("you idiot", "you idiot", null);
    guards("you idiot", true, null);
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq("you idiot"), anyString(), anyInt()))
        .willReturn(reply("Not okay.", null));
    send("you idiot");
    judges("Sorry", "Sorry", null);
    repliesTo("Sorry", reply("It's fine.", null));

    send("Sorry");

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiWorkerClient).should().guard(any(), any(), history.capture(), eq("Sorry"));
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
    then(aiWorkerClient).should().judge(any(), any(), any(), history.capture(), eq("Yes"));
    assertThat(history.getValue()).containsExactly(WorkerLine.npc(OPENING));
  }

  @Test
  @DisplayName("의도 판정이 고른 대화 사건을 학습자 턴에 저장하고 다음 판정·대답의 history로 보낸다")
  void send_savesJudgeEventsAndSendsThemInHistory() {
    given(aiWorkerClient.judge(any(), any(), any(), any(), eq("Hi!")))
        .willReturn(
            new JudgeResult(
                "Hi!", false, null, null, List.of(usage("intent")), null, List.of("GREETING")));
    guards("Hi!", false, null);
    repliesTo("Hi!", reply("Hello!", null));
    judges("Bye", "Bye", null);
    repliesTo("Bye", reply("See you!", null));

    send("Hi!");
    send("Bye");

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiWorkerClient)
        .should()
        .reply(any(), any(), any(), history.capture(), eq("Bye"), anyString(), anyInt());
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
    then(aiWorkerClient).should().judge(any(), any(), any(), any(), anyString());
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
    given(aiWorkerClient.judge(any(), any(), any(), any(), eq("*waves*")))
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
    then(aiWorkerClient)
        .should(never())
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt());
  }

  @Test
  @DisplayName("NPC 대답을 만들지 못하면 워커 오류 코드로 실패하고 턴을 저장하지 않으며 호출 기록을 FAILED로 남긴다")
  void send_replyFails_throwsWithoutTurns() {
    judges("Yes", "Yes", null);
    repliesTo("Yes", new NpcReply(null, null, "timeout", List.of(usage("npc"))));
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
    assertThat(record(requestId, AiRequestKind.NPC)).isEqualTo(AiRequestStatus.FAILED);
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
    then(aiWorkerClient).should(never()).judge(any(), any(), any(), any(), anyString());
  }

  @Test
  @DisplayName("AI 대답을 기다리는 사이 대화가 끝났으면 턴을 저장하지 않고 CONVERSATION_NOT_ACTIVE다")
  void send_conversationEndedWhileWaiting_throwsNotActive() {
    judges("Yes", "Yes", null);
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq("Yes"), eq(""), anyInt()))
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
  @DisplayName("무례 판정 3번으로 NPC가 대화를 끝내도 세 번 모두 깎는다")
  void send_abusiveUntilLimit_deductsEveryTime() {
    for (int count = 1; count <= ConversationSupport.RUDE_LIMIT; count++) {
      rudeTo("you idiot " + count, null);
    }

    send("you idiot 1");
    send("you idiot 2");
    send("you idiot 3");

    assertThat(affinity()).isEqualTo(-6 * ConversationSupport.RUDE_LIMIT);
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
    given(aiWorkerClient.judge(any(), any(), any(), any(), eq(text)))
        .willReturn(
            new JudgeResult(
                judgedText,
                false,
                null,
                english == null ? null : new JudgeResult.Translation(english, null),
                List.of(usage("intent"))));
    guards(text, false, null);
  }

  // 같은 문장에 다시 부르면 마지막 판정값으로 바뀐다.
  private void guards(String text, Boolean abusive, String errorCode) {
    given(aiWorkerClient.guard(any(), any(), any(), eq(text)))
        .willReturn(
            new GuardResult(new GuardResult.Guard(abusive, errorCode), List.of(usage("guard"))));
  }

  private void rudeTo(String text, Integer affinityDelta) {
    judges(text, text, null);
    given(aiWorkerClient.guard(any(), any(), any(), eq(text)))
        .willReturn(
            new GuardResult(
                new GuardResult.Guard(true, null), affinityDelta, List.of(usage("guard"))));
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq(text), anyString(), anyInt()))
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
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq(text), eq(""), anyInt()))
        .willReturn(reply);
  }

  private AiRequestStatus record(UUID requestId, AiRequestKind kind) {
    return aiRequestRepository.findByRequestIdAndKind(requestId, kind).orElseThrow().getStatus();
  }
}
