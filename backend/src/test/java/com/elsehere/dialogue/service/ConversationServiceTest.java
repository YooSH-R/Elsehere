package com.elsehere.dialogue.service;

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
import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.FollowUpResult;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.MemoryCardResult;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerFollowUp;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.common.config.AdjustableClock;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogCapture;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.ConversationDetailResponse;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.SpeechStatus;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.followup.entity.FollowUpWait;
import com.elsehere.followup.entity.NpcFollowUp;
import com.elsehere.followup.repository.NpcFollowUpRepository;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.AfterEach;
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

  @Autowired private TurnRepository turnRepository;

  @Autowired private TurnService turnService;

  @Autowired private SpeechService speechService;

  @Autowired private MemoryService memoryService;

  @Autowired private AiGateway aiGateway;

  @Autowired private ConversationRepository conversationRepository;

  @Autowired private FirstDayMissionRepository firstDayMissionRepository;
  @Autowired private TownStoryRepository townStoryRepository;

  @Autowired private EntityManager entityManager;

  @Autowired private AdjustableClock clock;

  @Autowired private FollowUpService followUpService;

  @Autowired private NpcFollowUpRepository followUpRepository;

  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
  }

  @AfterEach
  void resetClock() {
    clock.setInstant(TestClockConfig.NOW);
  }

  @Test
  @DisplayName("John은 저장된 랩으로 시작하며 첫 인사와 음성을 AI로 생성하지 않는다")
  void start_johnUsesRecordedRapWithoutAiGreeting() {
    UUID requestId = UUID.randomUUID();
    ConversationStartResponse response =
        conversationService.start(
            userId, new StartConversationRequest(requestId, "TOWN_JOHN_PARK", InitiatedBy.USER));
    assertThat(response.openingTurn().text()).startsWith("Cheeseburger, fries, lemonade on ice!");
    Turn recordedTurn = entityManager.find(Turn.class, response.openingTurn().turnId());
    assertThat(recordedTurn.getMeaningKo()).startsWith("치즈버거, 감자튀김, 얼음 넣은 레모네이드!");
    assertThat(recordedTurn.getGlossary()).isEqualTo("[]");
    then(aiGateway).shouldHaveNoInteractions();
    assertThat(
            conversationService
                .start(
                    userId,
                    new StartConversationRequest(requestId, "TOWN_JOHN_PARK", InitiatedBy.USER))
                .conversationId())
        .isEqualTo(response.conversationId());
  }

  @Test
  void start_savesGreetingEmotionsEvenWhenSynthesisTimedOut() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(
            new com.elsehere.ai.client.NpcReply(
                "Really?",
                null,
                null,
                List.of(),
                List.of(new com.elsehere.ai.client.WorkerSpeechPhrase("Really?", "curious", 0.7))));

    ConversationStartResponse response =
        conversationService.start(
            userId, new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC));
    entityManager.flush();
    entityManager.clear();

    var saved =
        entityManager.find(
            com.elsehere.conversation.entity.Turn.class, response.openingTurn().turnId());
    assertThat(saved.getNpcAudioKey()).isNull();
    assertThat(saved.getSpeechPhrases()).contains("curious", "Really?", "0.7");
  }

  @Test
  @DisplayName(
      "말걸기 대사가 없는 직원은 NPC가 먼저 말을 건 대화라도 AI가 만든 첫 인사로 시작하고 음성을 저장한다. 첫 인사는 대화를 저장하기 전에 만들므로 워커 요청에는 요청 ID만 붙인다")
  void start_staffWithoutApproachLine_usesAiGreeting() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi! What can I get for you?", "audio-1"));
    given(aiGateway.audio(any(), eq("audio-1"))).willReturn(WAV);
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
    then(aiGateway)
        .should()
        .reply(
            eq(new WorkerTrace(requestId, userId, null)),
            any(),
            any(),
            any(),
            anyString(),
            anyString(),
            anyInt(),
            any());
  }

  @Test
  @DisplayName("카페 휴식 단계에 택시기사와 대화를 시작해도 첫 인사 지시에 카페 주문 지시가 섞이지 않는다")
  void start_taxiDuringCafeBreak_greetingKeepsDriverRole() {
    FirstDayMission mission = FirstDayMission.start(userId);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    firstDayMissionRepository.saveAndFlush(mission);
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Where are you headed?", null));

    conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "TAXI_DRIVER", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
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
  @DisplayName("첫날 미션 직원도 성격에 맞게 인사하고 학습자가 할 요청을 먼저 꺼내지 않는다")
  void start_missionStaffGreeting_preservesCharacterWithoutSpoilingTasks() {
    firstDayMissionRepository.saveAndFlush(FirstDayMission.start(userId));
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Good morning! How can I help you?", null));

    conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "HOTEL_STAFF", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
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
        .contains("personality", "first-meeting", "Invite their request")
        .contains("Do not mention the learner's tasks, your offers or directions yet");
  }

  @Test
  @DisplayName("근무 중인 직원의 응대 상황과 성격을 살려 첫 인사를 만든다")
  void start_onDutyStaff_sendsServiceSceneAndCharacterGreeting() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi! What can I get for you?", null));

    startCafe();

    ArgumentCaptor<WorkerNpc> npc = ArgumentCaptor.forClass(WorkerNpc.class);
    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
        .should()
        .reply(
            any(),
            npc.capture(),
            any(),
            any(),
            anyString(),
            instruction.capture(),
            anyInt(),
            any());
    assertThat(npc.getValue().scene())
        .startsWith("The counter of the corner cafe.")
        .contains("Current game time: day 1, 08:00");
    assertThat(instruction.getValue())
        .contains("personality", "first-meeting", "Only offer service");
  }

  @Test
  @DisplayName("근무가 아닌 NPC와 시작한 대화는 근무가 아닌 대화로 저장한다")
  void start_npcOffDuty_savesOffDutyConversation() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Oh, hey there!", null));

    ConversationStartResponse response = startOffDuty("CAFE_STAFF", InitiatedBy.USER);

    assertThat(
            conversationRepository.findById(response.conversationId()).orElseThrow().isNpcOffDuty())
        .isTrue();
  }

  @Test
  @DisplayName("근무가 아닌 직원의 첫 인사는 응대 상황 대신 일상 상황으로 만들고 무엇을 도와줄지 묻지 않게 한다")
  void start_offDutyStaff_greetsCasuallyInOffDutyScene() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Oh, hey there!", null));

    ConversationStartResponse response = startOffDuty("CAFE_STAFF", InitiatedBy.USER);

    ArgumentCaptor<WorkerNpc> npc = ArgumentCaptor.forClass(WorkerNpc.class);
    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
        .should()
        .reply(
            any(),
            npc.capture(),
            any(),
            any(),
            anyString(),
            instruction.capture(),
            anyInt(),
            any());
    assertThat(npc.getValue().scene())
        .contains("Mia is off work right now")
        .contains("does not take orders or serve anyone");
    assertThat(instruction.getValue()).contains("off work").doesNotContain("how you can help");
    assertThat(response.openingTurn().text()).isEqualTo("Oh, hey there!");
  }

  @Test
  @DisplayName("근무가 아닌 NPC가 먼저 말을 걸어도 저장된 말걸기 대사 대신 AI가 만든 일상 인사로 시작한다")
  void start_offDutyPasserbyApproach_doesNotUseApproachLine() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Oh, hello!", null));

    ConversationStartResponse response = startOffDuty("TOWN_PASSERBY", InitiatedBy.NPC);

    assertThat(response.openingTurn().text()).isEqualTo("Oh, hello!");
    then(aiGateway)
        .should()
        .reply(
            any(),
            argThat(npc -> npc.scene().contains("Ben is off work right now")),
            any(),
            any(),
            anyString(),
            anyString(),
            anyInt(),
            any());
  }

  @Test
  @DisplayName("근무가 아닌 NPC와의 대화는 발화에 대답할 때도 워커에 일상 상황을 보낸다")
  void send_offDutyConversation_sendsOffDutySceneToReply() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Oh, hey there!", null));
    ConversationStartResponse started = startOffDuty("CAFE_STAFF", InitiatedBy.USER);

    sendText(started.conversationId(), "Are you working today?");

    then(aiGateway)
        .should()
        .reply(
            any(),
            argThat(npc -> npc.scene().contains("Mia is off work right now")),
            any(),
            any(),
            eq("Are you working today?"),
            anyString(),
            anyInt(),
            any());
  }

  @Test
  void start_townStoryStaffGreetingUsesTheCurrentStorySituation() {
    townStoryRepository.saveAndFlush(TownStory.start(userId));
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Good morning! How can I help you?", null));

    conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "DINER_STAFF", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
        .should()
        .reply(
            any(),
            argThat(npc -> "DINER_STAFF".equals(npc.id())),
            any(),
            any(),
            anyString(),
            instruction.capture(),
            anyInt(),
            any());
    assertThat(instruction.getValue()).containsIgnoringCase("spill");
  }

  @Test
  void dinerConversationsKeepTheirOwnGoalsAfterTheStoryAdvances() {
    TownStory story = townStoryRepository.saveAndFlush(TownStory.start(userId));
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("How can I help you?", null));

    var order =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "DINER_STAFF", InitiatedBy.USER));
    conversationRepository
        .findById(order.conversationId())
        .orElseThrow()
        .endByUser(clock.instant());
    story.accept(0, "DINER_STAFF", List.of("order_breakfast"));
    story.receiveDinerMeal();

    var spill =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "DINER_STAFF", InitiatedBy.USER));
    story.accept(0, "DINER_STAFF", List.of("explain_spill"));
    entityManager.flush();
    entityManager.clear();

    assertThat(townStoryRepository.findById(userId).orElseThrow().getStage()).isEqualTo(1);
    assertThat(
            conversationRepository
                .findById(order.conversationId())
                .orElseThrow()
                .getTownStoryGoalIds())
        .containsExactly("order_breakfast");
    assertThat(
            conversationRepository
                .findById(spill.conversationId())
                .orElseThrow()
                .getTownStoryGoalIds())
        .containsExactly("explain_spill");
  }

  @Test
  void firstDayReopenedConversationsKeepPendingGoalsAfterRest() {
    FirstDayMission mission = FirstDayMission.start(userId);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    mission.accept(1, "CAFE_STAFF", List.of("order_drink"));
    mission = firstDayMissionRepository.saveAndFlush(mission);
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("How can I help you?", null));

    var pending =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER));
    conversationRepository
        .findById(pending.conversationId())
        .orElseThrow()
        .endByUser(clock.instant());
    mission.accept(1, "CAFE_STAFF", List.of("set_options", "dine_in"));
    var waiting =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER));
    mission.restAtCafe();
    entityManager.flush();
    entityManager.clear();

    assertThat(firstDayMissionRepository.findById(userId).orElseThrow().getStage()).isEqualTo(2);
    assertThat(
            conversationRepository
                .findById(pending.conversationId())
                .orElseThrow()
                .getFirstDayGoalIds())
        .containsExactly("set_options", "dine_in");
    assertThat(
            conversationRepository
                .findById(waiting.conversationId())
                .orElseThrow()
                .getFirstDayGoalIds())
        .isEmpty();
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
    then(aiGateway)
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
    assertThat(response.conversation().rudeLimit()).isEqualTo(Conversation.RUDE_LIMIT);
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
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", "audio-1"));
    given(aiGateway.audio(any(), eq("audio-1")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER));

    assertThat(response.openingTurn().speech().status()).isEqualTo(SpeechStatus.FAILED);
    assertThat(response.openingTurn().speech().url()).isNull();
  }

  @Test
  @DisplayName("첫 인사를 만들지 못하면 그 오류로 실패하고 대화를 만들지 않는다")
  void start_greetingFails_throwsWithoutConversation() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_RATE_LIMITED));
    UUID requestId = UUID.randomUUID();

    assertThatThrownBy(
            () ->
                conversationService.start(
                    userId, new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.NPC)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_RATE_LIMITED));
    assertThat(conversationService.active(userId)).isEmpty();
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
  @DisplayName("같은 요청 ID로 대화 시작을 다시 보내면(응답 시간 초과 뒤 재시도) 새로 만들지 않고 처음 시작한 대화를 그대로 돌려준다")
  void start_sameRequestIdAgain_returnsSameConversation() {
    StartConversationRequest request =
        new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC);
    ConversationStartResponse first = conversationService.start(userId, request);

    ConversationStartResponse again = conversationService.start(userId, request);

    assertThat(again).usingRecursiveComparison().isEqualTo(first);
    assertThat(conversationRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("첫 인사를 기다리는 사이 같은 요청 ID의 재요청이 먼저 대화를 저장했으면, 처음 요청도 거절하지 않고 그 대화를 돌려준다")
  void start_sameRequestIdSavedWhileGreeting_returnsThatConversation() {
    StartConversationRequest request =
        new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC);
    AtomicBoolean retrying = new AtomicBoolean();
    AtomicReference<ConversationStartResponse> retried = new AtomicReference<>();
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              // 처음 요청이 첫 인사를 기다리는 동안 재요청이 끝까지 실행된다.
              if (retrying.compareAndSet(false, true)) {
                retried.set(conversationService.start(userId, request));
              }
              return reply("Hi! What can I get for you?", null);
            });

    ConversationStartResponse first = conversationService.start(userId, request);

    assertThat(first.conversationId()).isEqualTo(retried.get().conversationId());
    assertThat(conversationRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("다른 사용자가 시작한 대화의 요청 ID로 대화를 시작하면 REQUEST_ID_CONFLICT로 거절하고 그 대화를 돌려주지 않는다")
  void start_otherUsersRequestId_throwsRequestIdConflict() {
    StartConversationRequest request =
        new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC);
    conversationService.start(userId, request);
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");

    assertThatThrownBy(() -> conversationService.start(otherUserId, request))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.REQUEST_ID_CONFLICT));
  }

  @Test
  @DisplayName("첫 인사를 만드는 사이 다른 요청이 대화를 시작했으면 CONVERSATION_ALREADY_ACTIVE로 거절하고 진행 중인 대화는 하나만 남는다")
  void start_otherStartWhileGreeting_throwsAlreadyActive() {
    // 첫 인사(AI)를 기다리는 동안 같은 사용자의 다른 시작 요청이 먼저 저장된다.
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
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
      talkWithPasserby(guestId);
    }
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              talkWithPasserby(guestId);
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
  @DisplayName("게스트가 마지막 횟수로 시작한 대화를 같은 요청 ID로 다시 보내면 AI_QUOTA_EXHAUSTED로 막지 않고 그 대화를 돌려준다")
  void start_guestRetriesLastConversation_returnsSameConversation() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT - 1; i++) {
      talkWithPasserby(guestId);
    }
    StartConversationRequest request =
        new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC);
    ConversationStartResponse last = conversationService.start(guestId, request);

    ConversationStartResponse again = conversationService.start(guestId, request);

    assertThat(again.conversationId()).isEqualTo(last.conversationId());
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
  @DisplayName("게스트는 정해진 횟수만큼 대화를 주고받으면 더 시작할 수 없고 AI_QUOTA_EXHAUSTED로 거절된다")
  void start_guestReachedLimit_throwsQuotaExhausted() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT; i++) {
      talkWithPasserby(guestId);
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
  @DisplayName("가입 사용자는 게스트 한도보다 많이 대화해도 새 대화를 시작할 수 있다")
  void start_memberBeyondGuestLimit_starts() {
    for (int i = 0; i <= GUEST_CONVERSATION_LIMIT; i++) {
      talkWithPasserby(userId);
    }

    startAndEndPasserby(userId);

    assertThat(conversationRepository.count()).isEqualTo(GUEST_CONVERSATION_LIMIT + 2);
  }

  @Test
  @DisplayName("게스트가 말을 주고받지 않고 끝낸 대화는 횟수로 세지 않아 계속 새 대화를 시작할 수 있다")
  void start_guestConversationsWithoutExchange_doNotCount() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT; i++) {
      startAndEndPasserby(guestId);
    }

    startAndEndPasserby(guestId);

    assertThat(conversationRepository.count()).isEqualTo(GUEST_CONVERSATION_LIMIT + 1);
  }

  @Test
  @DisplayName("게스트가 대화 한도에 닿아 거절되면 사용자 ID와 한도를 INFO로 남긴다")
  void start_guestReachedLimit_logsLimitReached() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    for (int i = 0; i < GUEST_CONVERSATION_LIMIT; i++) {
      talkWithPasserby(guestId);
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
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiGateway.memoryCard(any(), any(), anyString(), any(), any()))
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
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiGateway.memoryCard(any(), any(), anyString(), any(), any()))
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
  @DisplayName("5분 뒤 종료 요청은 사용자 종료로 처리하고 피드백과 기억을 중복 생성하지 않는다")
  void end_afterTimeLimit_generatesFeedbackAndMemoryOnce() {
    ConversationStartResponse started = startPasserby();
    sendText(started.conversationId(), "Yes, it's sunny!");
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiGateway.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult("{\"name\": \"\"}", List.of()));
    clock.setInstant(TestClockConfig.NOW.plusSeconds(300));

    EndConversationResponse first =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));
    EndConversationResponse retried =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));

    assertThat(first.status()).isEqualTo(ConversationStatus.ENDED_BY_USER);
    assertThat(first.endReason()).isEqualTo(ConversationEndReason.USER_ENDED);
    assertThat(retried.endReason()).isEqualTo(ConversationEndReason.USER_ENDED);
    WorkerMemory memory = memoryService.memoryFor(userId, npcId(entityManager, "TOWN_PASSERBY"));
    assertThat(memory.visits()).isEqualTo(1);
    then(aiGateway).should().feedback(any(), any());
    then(aiGateway).should().memoryCard(any(), any(), anyString(), any(), any());
  }

  @Test
  @DisplayName("AI 첫인사에 사용자의 닉네임과 캐릭터를 실어 NPC가 캐릭터에 맞는 호칭을 쓰게 한다")
  void start_aiGreeting_sendsTheLearner() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", null));

    startCafe();

    ArgumentCaptor<WorkerLearner> learner = ArgumentCaptor.forClass(WorkerLearner.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), learner.capture());
    assertThat(learner.getValue()).isEqualTo(new WorkerLearner("Sun", "F", 1.1));
  }

  @Test
  @DisplayName("AI 첫인사는 학습자가 설정창에서 고른 NPC 말 빠르기로 말한다")
  void start_aiGreeting_sendsTheChosenSpeechSpeed() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", null));

    conversationService.start(
        userId,
        new StartConversationRequest(
            UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC, false, null, 0.9));

    ArgumentCaptor<WorkerLearner> learner = ArgumentCaptor.forClass(WorkerLearner.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), learner.capture());
    assertThat(learner.getValue().speechSpeed()).isEqualTo(0.9);
  }

  @Test
  @DisplayName("다시 만난 NPC의 첫 인사에는 이전 대화에서 저장한 기억을 넘긴다")
  void start_afterPreviousConversation_sendsMemory() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi!", null));
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiGateway.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult("{\"attitude\": \"friendly\"}", List.of()));
    ConversationStartResponse first = startCafe();
    sendText(first.conversationId(), "A latte, please.");
    conversationService.end(
        userId, first.conversationId(), new EndConversationRequest(UUID.randomUUID(), true));

    startCafe();

    ArgumentCaptor<WorkerMemory> memory = ArgumentCaptor.forClass(WorkerMemory.class);
    then(aiGateway)
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
    assertThat(detail.rudeLimit()).isEqualTo(Conversation.RUDE_LIMIT);

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

  @Test
  @DisplayName("NPC가 곧 건네기로 한 것이 있으면 대화 종료 응답에 남은 초와 함께 담는다")
  void end_orderBeingPrepared_returnsFollowUp() {
    ConversationStartResponse started = startPasserby();
    sendText(started.conversationId(), "One iced latte, please.");
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiGateway.followUp(any(), any(), any()))
        .willReturn(
            new FollowUpResult(
                new WorkerFollowUp("iced latte", "아이스 라테", "QUICK"), List.of(usage("follow_up"))));

    EndConversationResponse response =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));

    assertThat(response.followUp()).isNotNull();
    assertThat(response.followUp().itemKo()).isEqualTo("아이스 라테");
    assertThat(response.followUp().remainingSeconds()).isEqualTo(45);
    assertThat(response.followUp().ready()).isFalse();
  }

  @Test
  @DisplayName("건넬 것이 없으면 대화 종료 응답의 followUp은 null이다")
  void end_nothingOwed_hasNoFollowUp() {
    ConversationStartResponse started = startPasserby();
    sendText(started.conversationId(), "Yes, it's sunny!");
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));
    given(aiGateway.followUp(any(), any(), any())).willReturn(new FollowUpResult(null, List.of()));

    EndConversationResponse response =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));

    assertThat(response.followUp()).isNull();
  }

  @Test
  @DisplayName("준비된 것이 있는 NPC와 대화를 시작하면 건네는 첫마디로 열고 건넨 것을 알린다. 같은 요청 ID로 다시 보내도 같다")
  void start_readyFollowUp_opensByHandingItOver() {
    NpcFollowUp latte = readyLatte();
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Here's your iced latte! Enjoy.", null));
    UUID requestId = UUID.randomUUID();

    ConversationStartResponse response =
        conversationService.start(
            userId, new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.NPC));

    assertThat(response.deliveredFollowUpId()).isEqualTo(latte.getId());
    assertThat(response.openingTurn().text()).isEqualTo("Here's your iced latte! Enjoy.");
    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue()).contains("iced latte", "is ready now");
    assertThat(followUpService.waiting(userId)).isEmpty();
    assertThat(
            conversationService
                .start(
                    userId, new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.NPC))
                .deliveredFollowUpId())
        .isEqualTo(latte.getId());
  }

  @Test
  @DisplayName("출동 결과가 준비된 경찰관과 대화를 시작하면 순찰을 다녀와 결과를 알리는 첫마디로 연다")
  void start_readyPoliceCallResult_opensByReportingIt() {
    followUpRepository.saveAndFlush(
        NpcFollowUp.promise(
            userId,
            npcId(entityManager, "POLICE_OFFICER"),
            999L,
            "your wallet, found at Maple Diner",
            "지갑 수색 결과",
            FollowUpWait.SHORT,
            clock.instant().minusSeconds(120)));
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("I'm back! I found your wallet at the diner. Here you go.", null));

    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "POLICE_OFFICER", InitiatedBy.NPC));

    assertThat(response.deliveredFollowUpId()).isNotNull();
    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue())
        .contains("back from the call", "your wallet, found at Maple Diner")
        .doesNotContain("is ready now");
  }

  @Test
  @DisplayName("아직 준비되지 않았으면 평소 첫 인사로 시작하고 건네지 않는다")
  void start_followUpNotReadyYet_greetsAsUsual() {
    followUpRepository.saveAndFlush(
        NpcFollowUp.promise(
            userId,
            npcId(entityManager, "CAFE_STAFF"),
            999L,
            "iced latte",
            "아이스 라테",
            FollowUpWait.QUICK,
            clock.instant()));
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi! What can I get for you?", null));

    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.USER));

    assertThat(response.deliveredFollowUpId()).isNull();
    assertThat(followUpService.waiting(userId)).hasSize(1);
  }

  // 1분 전에 약속해 이미 준비된 아이스 라테(QUICK 45초)
  private NpcFollowUp readyLatte() {
    return followUpRepository.saveAndFlush(
        NpcFollowUp.promise(
            userId,
            npcId(entityManager, "CAFE_STAFF"),
            999L,
            "iced latte",
            "아이스 라테",
            FollowUpWait.QUICK,
            clock.instant().minusSeconds(60)));
  }

  private ConversationStartResponse startPasserby() {
    return conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));
  }

  // 피드백·기억 갱신 없이 대화만 끝낸다. 말을 주고받지 않았으므로 게스트 횟수에 들어가지 않는다.
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

  // 한 번 주고받고 끝낸다. 게스트 횟수는 한 번이라도 주고받은 대화만 센다. 발화 처리처럼 사용자 턴과 NPC 대답을 함께 저장한다.
  private void talkWithPasserby(Long ownerId) {
    ConversationStartResponse started =
        conversationService.start(
            ownerId,
            new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC));
    Conversation conversation =
        conversationRepository.findById(started.conversationId()).orElseThrow();
    int lastSeq = turnRepository.findLastSeq(conversation.getId());
    turnRepository.save(
        Turn.user(
            conversation,
            lastSeq + 1,
            UUID.randomUUID(),
            InputType.TEXT,
            "Where is the cafe?",
            null,
            null,
            false,
            null,
            null));
    turnRepository.save(
        Turn.npc(conversation, lastSeq + 2, "It's right over there.", null, clock.instant()));
    conversation.endByUser(clock.instant());
  }

  private ConversationStartResponse startCafe() {
    return conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "CAFE_STAFF", InitiatedBy.NPC));
  }

  private ConversationStartResponse startOffDuty(String npcCode, InitiatedBy initiatedBy) {
    return conversationService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), npcCode, initiatedBy, true));
  }

  private void sendText(Long conversationId, String text) {
    given(aiGateway.judge(any(), any(), any(), any(), eq(text)))
        .willReturn(new JudgeResult(text, false, null, null, List.of(usage("judge"))));
    given(aiGateway.guard(any(), any(), any(), eq(text)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of(usage("guard"))));
    given(aiGateway.reply(any(), any(), any(), any(), eq(text), eq(""), anyInt(), any()))
        .willReturn(reply("Nice!", null));
    turnService.send(
        userId, conversationId, new TurnRequest(UUID.randomUUID(), InputType.TEXT, text, null));
  }
}
