package com.elsehere.conversation.service;

import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.reply;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.argThat;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.atLeastOnce;
import static org.mockito.Mockito.never;

import ch.qos.logback.classic.Level;
import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.MemoryCardResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogCapture;
import com.elsehere.conversation.dto.ConversationDetailResponse;
import com.elsehere.conversation.dto.ConversationStartResponse;
import com.elsehere.conversation.dto.EndConversationRequest;
import com.elsehere.conversation.dto.EndConversationResponse;
import com.elsehere.conversation.dto.SpeechStatus;
import com.elsehere.conversation.dto.StartConversationRequest;
import com.elsehere.conversation.dto.TurnRequest;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;

// 대화 시작(CONV-01)·종료(CONV-08)·조회(CONV-11). AI 워커는 Mock이고 저장 결과는 응답과 조회 API로 확인한다.
@DialogueServiceTest
class ConversationServiceTest {

  private static final byte[] WAV = {82, 73, 70, 70};

  // DialogueServiceTest에 설정한 elsehere.guest.conversation-limit
  private static final int GUEST_CONVERSATION_LIMIT = 2;

  @Autowired private ConversationService conversationService;

  @Autowired private TurnService turnService;

  @Autowired private SpeechService speechService;

  @Autowired private MemoryService memoryService;

  @Autowired private AiWorkerClient aiWorkerClient;

  @Autowired private ConversationRepository conversationRepository;

  @Autowired private AiRequestRepository aiRequestRepository;

  @Autowired private FirstDayMissionRepository firstDayMissionRepository;

  @Autowired private EntityManager entityManager;

  @Autowired private Clock clock;

  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
  }

  @Test
  @DisplayName("말걸기 대사가 없는 직원은 NPC가 먼저 말을 건 대화라도 AI가 만든 첫 인사로 시작하고 음성을 저장한다")
  void start_staffWithoutApproachLine_usesAiGreeting() {
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi! What can I get for you?", "audio-1"));
    given(aiWorkerClient.audio(any(), eq("audio-1"))).willReturn(WAV);
    UUID requestId = UUID.randomUUID();

    ConversationStartResponse response =
        conversationService.start(
            userId, new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.NPC));

    assertThat(response.status()).isEqualTo(ConversationStatus.ACTIVE);
    assertThat(response.levelApplied()).isEqualTo(1);
    assertThat(response.npc().npcCode()).isEqualTo("CAFE_STAFF");
    assertThat(response.npc().name()).isEqualTo("Mia");
    assertThat(response.openingTurn().seq()).isEqualTo(1);
    assertThat(response.openingTurn().text()).isEqualTo("Hi! What can I get for you?");
    assertThat(response.openingTurn().suggestWords()).isNull();
    assertThat(response.openingTurn().speech().status()).isEqualTo(SpeechStatus.READY);
    assertThat(speechService.npcSpeech(userId, response.openingTurn().turnId())).isEqualTo(WAV);
    assertThat(
            aiRequestRepository
                .findByRequestIdAndKind(requestId, AiRequestKind.NPC)
                .orElseThrow()
                .getStatus())
        .isEqualTo(AiRequestStatus.OK);
  }

  @Test
  @DisplayName("카페 휴식 단계에 택시기사와 대화를 시작해도 첫 인사 지시에 카페 주문 지시가 섞이지 않는다")
  void start_taxiDuringCafeBreak_greetingKeepsDriverRole() {
    FirstDayMission mission = FirstDayMission.start(userId);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    firstDayMissionRepository.saveAndFlush(mission);
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Where are you headed?", null));

    conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "TAXI_DRIVER", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiWorkerClient)
        .should()
        .reply(
            any(),
            argThat(npc -> "TAXI_DRIVER".equals(npc.id())),
            any(),
            any(),
            anyString(),
            instruction.capture(),
            anyInt(),
            any());
    assertThat(instruction.getValue())
        .contains("Corner Cafe", "Stay in your own role")
        .doesNotContain("Offer drinks", "size, temperature or milk");
  }

  @Test
  @DisplayName("첫날 미션 직원의 첫 인사는 무엇을 도와줄지만 묻고 학습자가 할 요청을 먼저 꺼내지 않게 지시한다")
  void start_missionStaffGreeting_onlyAsksHowToHelp() {
    firstDayMissionRepository.saveAndFlush(FirstDayMission.start(userId));
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Good morning! How can I help you?", null));

    conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "HOTEL_STAFF", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiWorkerClient)
        .should()
        .reply(
            any(),
            argThat(npc -> "HOTEL_STAFF".equals(npc.id())),
            any(),
            any(),
            anyString(),
            instruction.capture(),
            anyInt(),
            any());
    assertThat(instruction.getValue())
        .contains("ask how you can help")
        .contains("Do not mention the learner's tasks, your offers or directions yet");
  }

  @Test
  @DisplayName("말걸기 대사가 있는 행인이 먼저 말을 걸었으면 그 대사로 시작하고 AI를 부르지 않는다")
  void start_passerbyApproach_usesApproachLineWithoutAi() {
    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));

    assertThat(response.openingTurn().text()).isEqualTo("Hi there! Nice weather today, isn't it?");
    assertThat(response.openingTurn().suggestWords()).isNull();
    then(aiWorkerClient)
        .should(never())
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any());
  }

  @Test
  @DisplayName("대화를 시작하면 무례 횟수 0·무례 한도와 그 NPC의 지금 호감도를 대화 상태로 돌려준다")
  void start_respondsRudeLimitAndCurrentAffinity() {
    entityManager
        .createNativeQuery(
            "insert into user_npc_relations (user_id, npc_id, affinity, talk_count)"
                + " values (?, ?, 12, 1)")
        .setParameter(1, userId)
        .setParameter(2, npcId(entityManager, "TOWN_PASSERBY"))
        .executeUpdate();

    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));

    assertThat(response.conversation().status()).isEqualTo(ConversationStatus.ACTIVE);
    assertThat(response.conversation().endReason()).isNull();
    assertThat(response.conversation().rudeCount()).isZero();
    assertThat(response.conversation().rudeLimit()).isEqualTo(ConversationSupport.RUDE_LIMIT);
    assertThat(response.conversation().affinity()).isEqualTo(12);
  }

  @Test
  @DisplayName("처음 만난 NPC와 대화를 시작하면 호감도는 0이다")
  void start_firstMeeting_respondsZeroAffinity() {
    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));

    assertThat(response.conversation().affinity()).isZero();
  }

  @Test
  @DisplayName("NPC 음성을 받지 못해도 대화는 시작하고 첫 턴의 음성 상태는 FAILED다")
  void start_audioDownloadFails_startsWithoutSpeech() {
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", "audio-1"));
    given(aiWorkerClient.audio(any(), eq("audio-1")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER));

    assertThat(response.openingTurn().speech().status()).isEqualTo(SpeechStatus.FAILED);
    assertThat(response.openingTurn().speech().url()).isNull();
  }

  @Test
  @DisplayName("첫 인사를 만들지 못하면 워커 오류 코드로 실패하고 대화를 만들지 않으며 호출 기록을 FAILED로 남긴다")
  void start_greetingFails_throwsWithoutConversation() {
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(new NpcReply(null, null, "provider_limit", List.of(usage("npc"))));
    UUID requestId = UUID.randomUUID();

    assertThatThrownBy(
            () ->
                conversationService.start(
                    userId, new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.NPC)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_RATE_LIMITED));
    assertThat(conversationService.active(userId)).isEmpty();
    assertThat(
            aiRequestRepository
                .findByRequestIdAndKind(requestId, AiRequestKind.NPC)
                .orElseThrow()
                .getStatus())
        .isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("진행 중인 대화가 있으면 CONVERSATION_ALREADY_ACTIVE와 진행 중인 대화 ID로 거절한다")
  void start_whileActive_throwsAlreadyActive() {
    ConversationStartResponse active = startPasserby();

    assertThatThrownBy(
            () ->
                conversationService.start(
                    userId,
                    new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> {
              assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.CONVERSATION_ALREADY_ACTIVE);
              assertThat(exception.getDetails())
                  .containsEntry("activeConversationId", active.conversationId());
            });
  }

  @Test
  @DisplayName("첫 인사를 만드는 사이 다른 요청이 대화를 시작했으면 CONVERSATION_ALREADY_ACTIVE로 거절하고 진행 중인 대화는 하나만 남는다")
  void start_otherStartWhileGreeting_throwsAlreadyActive() {
    // 첫 인사(AI)를 기다리는 동안 같은 사용자의 다른 시작 요청이 먼저 저장된다.
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              startPasserby();
              return reply("Hi! What can I get for you?", null);
            });

    assertThatThrownBy(this::startCafe)
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.CONVERSATION_ALREADY_ACTIVE));
    assertThat(conversationRepository.findAll()).filteredOn(Conversation::isActive).hasSize(1);
  }

  @Test
  @DisplayName("게스트가 첫 인사를 기다리는 사이 다른 요청으로 마지막 횟수를 쓰면 AI_QUOTA_EXHAUSTED로 거절하고 횟수를 넘지 않는다")
  void start_guestLimitReachedWhileGreeting_throwsQuotaExhausted() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT - 1; i++) {
      startAndEndPasserby(guestId);
    }
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              startAndEndPasserby(guestId);
              return reply("Hi! What can I get for you?", null);
            });

    assertThatThrownBy(
            () ->
                conversationService.start(
                    guestId,
                    new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_QUOTA_EXHAUSTED));
    assertThat(conversationRepository.count()).isEqualTo(GUEST_CONVERSATION_LIMIT);
  }

  @Test
  @DisplayName("닫힌 장소의 NPC와는 대화를 시작할 수 없다")
  void start_closedScene_throwsSceneClosed() {
    entityManager
        .createNativeQuery("update scenes set is_open = false where code = 'TOWN'")
        .executeUpdate();
    entityManager.clear();

    assertThatThrownBy(this::startPasserby)
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.SCENE_CLOSED));
  }

  @Test
  @DisplayName("없는 NPC 키면 NOT_FOUND다")
  void start_unknownNpc_throwsNotFound() {
    assertThatThrownBy(
            () ->
                conversationService.start(
                    userId,
                    new StartConversationRequest(UUID.randomUUID(), "MOON_NPC", InitiatedBy.USER)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("게스트는 정해진 횟수만큼 대화를 시작하면 더 시작할 수 없고 AI_QUOTA_EXHAUSTED로 거절된다")
  void start_guestReachedLimit_throwsQuotaExhausted() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT; i++) {
      startAndEndPasserby(guestId);
    }

    assertThatThrownBy(() -> startAndEndPasserby(guestId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> {
              assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_QUOTA_EXHAUSTED);
              // AI 사용량이 아니라 게스트 대화 횟수 때문에 막혔다고 알린다.
              assertThat(exception.getMessage())
                  .isEqualTo(
                      "게스트는 NPC와 "
                          + GUEST_CONVERSATION_LIMIT
                          + "번까지 대화할 수 있어요. 마을 탐험, 사물 카드, 단어장은 계속할 수 있어요.");
              assertThat(exception.getDetails())
                  .containsEntry("aiStatus", "AI_EXHAUSTED")
                  .containsEntry(
                      "allowedFeatures",
                      List.of("WORLD_EXPLORE", "OBJECT_CARD", "VOCABULARY", "SAVED_RECORDS"));
            });
    assertThat(conversationRepository.count()).isEqualTo(GUEST_CONVERSATION_LIMIT);
  }

  @Test
  @DisplayName("가입 사용자는 게스트 한도보다 많이 대화를 시작할 수 있다")
  void start_memberBeyondGuestLimit_starts() {
    for (int i = 0; i <= GUEST_CONVERSATION_LIMIT; i++) {
      startAndEndPasserby(userId);
    }

    assertThat(conversationRepository.count()).isEqualTo(GUEST_CONVERSATION_LIMIT + 1);
  }

  @Test
  @DisplayName("게스트가 대화 한도에 닿아 거절되면 사용자 ID와 한도를 INFO로 남긴다")
  void start_guestReachedLimit_logsLimitReached() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT; i++) {
      startAndEndPasserby(guestId);
    }

    try (LogCapture logs = LogCapture.atInfo(GuestConversationLimit.class)) {
      assertThatThrownBy(() -> startAndEndPasserby(guestId)).isInstanceOf(BusinessException.class);

      assertThat(logs.messages(Level.INFO))
          .containsExactly(
              "guest_conversation_limit_reached user_id="
                  + guestId
                  + " limit="
                  + GUEST_CONVERSATION_LIMIT);
    }
  }

  @Test
  @DisplayName("대화를 시작하면 대화 ID·NPC·먼저 건 쪽만 INFO로 남기고 첫 대사는 남기지 않는다")
  void start_logsConversationStarted() {
    try (LogCapture logs = LogCapture.atInfo(ConversationService.class)) {
      ConversationStartResponse started = startPasserby();

      assertThat(logs.messages(Level.INFO))
          .containsExactly(
              "conversation_started conversation_id="
                  + started.conversationId()
                  + " npc=TOWN_PASSERBY initiated_by=NPC");
    }
  }

  @Test
  @DisplayName("대화를 끝내면 종료 상태·이유·사용자 발화 수를 INFO로 남기고, 다시 끝내도 또 남기지 않는다")
  void end_logsConversationEndedOnce() {
    ConversationStartResponse started = startPasserby();
    sendText(started.conversationId(), "Yes, it's sunny!");
    given(aiWorkerClient.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult("{\"name\": \"\"}", List.of()));

    try (LogCapture logs = LogCapture.atInfo(ConversationService.class)) {
      conversationService.end(
          userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));
      conversationService.end(
          userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));

      assertThat(logs.messages(Level.INFO))
          .containsExactly(
              "conversation_ended conversation_id="
                  + started.conversationId()
                  + " status=ENDED_BY_USER end_reason=USER_ENDED user_turns=1");
    }
  }

  @Test
  @DisplayName("대화를 끝내면 ENDED_BY_USER와 사용자 발화 수, 피드백을 돌려주고 NPC 기억을 갱신한다")
  void end_savesEndAndReturnsFeedback() {
    ConversationStartResponse started = startPasserby();
    sendText(started.conversationId(), "Yes, it's sunny!");
    given(aiWorkerClient.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult("{\"name\": \"\"}", List.of()));

    EndConversationResponse response =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));

    assertThat(response.status()).isEqualTo(ConversationStatus.ENDED_BY_USER);
    assertThat(response.endReason()).isEqualTo(ConversationEndReason.USER_ENDED);
    assertThat(response.endedAt()).isNotNull();
    assertThat(response.sceneCode()).isEqualTo("TOWN");
    assertThat(response.userTurnCount()).isEqualTo(1);
    assertThat(response.feedback().status()).isEqualTo(FeedbackStatus.FAILED);
    assertThat(response.feedback().errorCode()).isEqualTo("AI_TIMEOUT");
    WorkerMemory memory = memoryService.memoryFor(userId, npcId(entityManager, "TOWN_PASSERBY"));
    assertThat(memory.visits()).isEqualTo(1);
  }

  @Test
  @DisplayName("AI 첫인사에 사용자의 닉네임과 캐릭터를 실어 NPC가 캐릭터에 맞는 호칭을 쓰게 한다")
  void start_aiGreeting_sendsTheLearner() {
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", null));

    startCafe();

    ArgumentCaptor<WorkerLearner> learner = ArgumentCaptor.forClass(WorkerLearner.class);
    then(aiWorkerClient)
        .should()
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), learner.capture());
    assertThat(learner.getValue()).isEqualTo(new WorkerLearner("Sun", "F"));
  }

  @Test
  @DisplayName("다시 만난 NPC의 첫 인사에는 이전 대화에서 저장한 기억을 넘긴다")
  void start_afterPreviousConversation_sendsMemory() {
    given(
            aiWorkerClient.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", null));
    given(aiWorkerClient.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult("{\"attitude\": \"friendly\"}", List.of()));
    ConversationStartResponse first = startCafe();
    sendText(first.conversationId(), "A latte, please.");
    conversationService.end(
        userId, first.conversationId(), new EndConversationRequest(UUID.randomUUID(), true));

    startCafe();

    ArgumentCaptor<WorkerMemory> memory = ArgumentCaptor.forClass(WorkerMemory.class);
    then(aiWorkerClient)
        .should(atLeastOnce())
        .reply(any(), any(), memory.capture(), any(), anyString(), anyString(), anyInt(), any());
    List<WorkerMemory> sent = memory.getAllValues();
    assertThat(sent.get(0)).isNull();
    WorkerMemory last = sent.get(sent.size() - 1);
    assertThat(last.visits()).isEqualTo(1);
    assertThat(last.card()).isEqualTo(Map.of("attitude", "friendly"));
  }

  @Test
  @DisplayName("진행 중인 대화 조회는 턴 목록을 돌려주고, 끝낸 뒤에는 비어 있다")
  void active_returnsTurnsUntilEnded() {
    ConversationStartResponse started = startPasserby();

    ConversationDetailResponse detail = conversationService.active(userId).orElseThrow();

    assertThat(detail.conversationId()).isEqualTo(started.conversationId());
    assertThat(detail.turns()).hasSize(1);
    assertThat(detail.rudeLimit()).isEqualTo(ConversationSupport.RUDE_LIMIT);

    conversationService.end(
        userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), true));
    assertThat(conversationService.active(userId)).isEmpty();
  }

  @Test
  @DisplayName("다른 사용자의 대화는 조회·종료 모두 NOT_FOUND다")
  void otherUsersConversation_throwsNotFound() {
    ConversationStartResponse started = startPasserby();
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");

    assertThatThrownBy(() -> conversationService.detail(otherUserId, started.conversationId()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThatThrownBy(
            () ->
                conversationService.end(
                    otherUserId,
                    started.conversationId(),
                    new EndConversationRequest(UUID.randomUUID(), true)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(conversationRepository.findById(started.conversationId()).orElseThrow().isActive())
        .isTrue();
  }

  private ConversationStartResponse startPasserby() {
    return conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));
  }

  // 피드백·기억 갱신 없이 대화만 끝낸다. 대화 횟수 제한은 끝난 대화도 센다.
  private void startAndEndPasserby(Long ownerId) {
    ConversationStartResponse started =
        conversationService.start(
            ownerId,
            new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));
    conversationRepository
        .findById(started.conversationId())
        .orElseThrow()
        .endByUser(clock.instant());
  }

  private ConversationStartResponse startCafe() {
    return conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC));
  }

  private void sendText(Long conversationId, String text) {
    given(aiWorkerClient.judge(any(), any(), any(), any(), eq(text)))
        .willReturn(new JudgeResult(text, false, null, null, List.of(usage("judge"))));
    given(aiWorkerClient.guard(any(), any(), any(), eq(text)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of(usage("guard"))));
    given(aiWorkerClient.reply(any(), any(), any(), any(), eq(text), eq(""), anyInt(), any()))
        .willReturn(reply("Nice!", null));
    turnService.send(
        userId, conversationId, new TurnRequest(UUID.randomUUID(), InputType.TEXT, text, null));
  }
}
