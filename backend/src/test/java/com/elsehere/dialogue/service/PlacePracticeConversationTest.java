package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.reply;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.argThat;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.atLeastOnce;
import static org.mockito.Mockito.clearInvocations;

import com.elsehere.ai.client.FeedbackInput;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.HintResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.dto.PracticeProgressResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.service.PlacePracticeService;
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
import org.springframework.test.context.bean.override.mockito.MockitoBean;

/**
 * 장소 연습 대화(PRC-02)를 실제 대화 서비스와 DB로 검증한다. 첫날 퀘스트의 지금 단계도 호텔 직원이 맡고 있어, 연습이 이야기 퀘스트를 섞지도 진행하지도 않는지 함께
 * 본다.
 */
@DialogueServiceTest
@MockitoBean(types = {MemoryService.class, FollowUpService.class})
class PlacePracticeConversationTest {
  private static final String SCENARIO = "HOTEL_CHANGE_ROOM";

  @Autowired private ConversationService conversationService;
  @Autowired private TurnService turnService;
  @Autowired private TurnHelpService turnHelpService;
  @Autowired private PlacePracticeService placePractice;
  @Autowired private FirstDayMissionRepository firstDayMissionRepository;
  @Autowired private EntityManager entityManager;
  @Autowired private AiGateway aiGateway;
  @Autowired private MemoryService memoryService;
  @Autowired private FollowUpService followUpService;
  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "practice-dialogue");
    firstDayMissionRepository.saveAndFlush(FirstDayMission.start(userId));
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Sure, what can I do for you?", null));
    given(aiGateway.guard(any(), any(), any(), anyString()))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
  }

  @Test
  @DisplayName("장소 연습 첫인사도 학습자가 고른 NPC 말 빠르기로 말한다")
  void start_greetsWithTheChosenSpeechSpeed() {
    conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.4);

    ArgumentCaptor<WorkerLearner> learner = ArgumentCaptor.forClass(WorkerLearner.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), learner.capture());
    assertThat(learner.getValue().speechSpeed()).isEqualTo(1.4);
  }

  @Test
  @DisplayName("장소 연습은 고른 상황으로 직원과 대화를 시작하고 이야기 퀘스트·기억·건넬 것을 섞지 않는다")
  void start_opensTheScenarioWithoutStoryQuestMemoryOrFollowUp() {
    UUID requestId = UUID.randomUUID();

    ConversationStartResponse started =
        conversationService.startPractice(userId, SCENARIO, requestId, 1.1);

    assertThat(started.practiceScenarioId()).isEqualTo(SCENARIO);
    assertThat(started.npc().npcCode()).isEqualTo("HOTEL_STAFF");
    assertThat(started.status()).isEqualTo(ConversationStatus.ACTIVE);
    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            isNull(),
            any(),
            anyString(),
            argThat(
                instruction ->
                    instruction.contains("Room 512")
                        && instruction.contains("describe_room_problem")
                        && !instruction.contains("suitcase")),
            anyInt(),
            any());
    then(memoryService).shouldHaveNoInteractions();
    then(followUpService).shouldHaveNoInteractions();
    Conversation saved = entityManager.find(Conversation.class, started.conversationId());
    assertThat(saved.getFirstDayStage()).isNull();
    assertThat(conversationService.startPractice(userId, SCENARIO, requestId, 1.1))
        .isEqualTo(started);
    assertThatThrownBy(
            () ->
                conversationService.start(
                    userId,
                    new StartConversationRequest(requestId, "HOTEL_STAFF", InitiatedBy.USER)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.REQUEST_ID_CONFLICT));
  }

  @Test
  @DisplayName("없는 상황은 AI를 부르지 않고 NOT_FOUND다")
  void start_unknownScenarioIsNotFound() {
    assertThatThrownBy(
            () ->
                conversationService.startPractice(
                    userId, "NO_SUCH_SCENARIO", UUID.randomUUID(), 1.1))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    then(aiGateway).shouldHaveNoInteractions();
  }

  @Test
  @DisplayName("연습 대화의 발화는 상황의 남은 목표로 판정해 이룬 목표만 기록하고, 이야기 퀘스트와 호감도는 바꾸지 않는다")
  void turn_judgesScenarioGoalsAndRecordsRecognizedOnes() {
    ConversationStartResponse started =
        conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.1);

    TurnResponse turn =
        say(
            started.conversationId(),
            "My room is too noisy.",
            "describe_room_problem",
            "store_suitcase");

    ArgumentCaptor<WorkerState> state = ArgumentCaptor.forClass(WorkerState.class);
    then(aiGateway).should().judge(any(), any(), state.capture(), any(), anyString());
    assertThat(state.getValue().id()).isEqualTo("practice_" + SCENARIO);
    assertThat(state.getValue().desc()).contains("Room 512");
    assertThat(state.getValue().accumulateHistory()).isTrue();
    assertThat(state.getValue().intents())
        .extracting(WorkerState.Intent::id)
        .containsExactly("describe_room_problem", "ask_other_room");
    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            isNull(),
            any(),
            anyString(),
            argThat(
                instruction ->
                    instruction.contains("This turn recognized: describe_room_problem.")
                        && instruction.contains(
                            "Remaining objectives after this turn: ask_other_room")),
            anyInt(),
            any());
    assertThat(turn.conversation().status()).isEqualTo(ConversationStatus.ACTIVE);
    assertThat(turn.conversation().affinity()).isZero();
    assertThat(progress(started).goals())
        .extracting(PracticeProgressResponse.Goal::completed)
        .containsExactly(true, false);
    FirstDayMission story = firstDayMissionRepository.findById(userId).orElseThrow();
    assertThat(story.getStage()).isZero();
    assertThat(story.hasGoal(1)).isFalse();
  }

  @Test
  @DisplayName("모든 목표를 이룬 턴에 직원이 마무리하며 대화를 끝내고 그 연습을 완료로 남긴다")
  void turn_completingEveryGoalWrapsUpAndRecordsDone() {
    ConversationStartResponse started =
        conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.1);
    say(started.conversationId(), "My room is too noisy.", "describe_room_problem");
    clearInvocations(aiGateway);

    TurnResponse last =
        say(started.conversationId(), "Can I move to another room?", "ask_other_room");

    assertThat(last.conversation().status()).isEqualTo(ConversationStatus.ENDED_BY_NPC);
    assertThat(last.conversation().endReason()).isEqualTo(ConversationEndReason.NPC_ENDED);
    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            any(),
            any(),
            anyString(),
            argThat(instruction -> instruction.endsWith(WrapUp.INSTRUCTION)),
            anyInt(),
            any());
    assertThat(progress(started).done()).isTrue();
    assertThat(placePractice.places(userId).places().getFirst().doneCount()).isEqualTo(1);
  }

  @Test
  @DisplayName("끝낸 연습을 다시 하면 목표를 처음부터 다시 판정하고, 완료 기록은 남는다")
  void start_againAfterFinishingBeginsWithFreshGoals() {
    ConversationStartResponse first =
        conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.1);
    say(
        first.conversationId(),
        "My room is noisy. Can I change rooms?",
        "describe_room_problem",
        "ask_other_room");
    clearInvocations(aiGateway);

    ConversationStartResponse again =
        conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.1);
    say(again.conversationId(), "Hello.");

    assertThat(again.conversationId()).isNotEqualTo(first.conversationId());
    PracticeProgressResponse progress = progress(again);
    assertThat(progress.goals()).noneMatch(PracticeProgressResponse.Goal::completed);
    assertThat(progress.done()).isFalse();
    ArgumentCaptor<WorkerState> state = ArgumentCaptor.forClass(WorkerState.class);
    then(aiGateway).should().judge(any(), any(), state.capture(), any(), anyString());
    assertThat(state.getValue().intents()).hasSize(2);
    assertThat(placePractice.places(userId).places().getFirst().scenarios().getFirst().done())
        .isTrue();
  }

  @Test
  @DisplayName("연습 대화를 끝내면 상황과 목표로 피드백을 만들고 기억·건넬 것은 만들지 않는다")
  void end_givesFeedbackOnTheScenarioWithoutMemoryOrFollowUp() {
    ConversationStartResponse started =
        conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.1);
    say(started.conversationId(), "My room is too noisy.", "describe_room_problem");
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of()));

    var ended =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));

    assertThat(ended.feedback()).isNotNull();
    ArgumentCaptor<FeedbackInput> input = ArgumentCaptor.forClass(FeedbackInput.class);
    then(aiGateway).should().feedback(any(), input.capture());
    assertThat(input.getValue().context().mode()).isEqualTo("quest");
    assertThat(input.getValue().context().sceneDescription())
        .contains("방 바꿔 달라고 하기", "방의 문제 말하기", "다른 방 부탁하기");
    then(memoryService).shouldHaveNoInteractions();
    then(followUpService).shouldHaveNoInteractions();
  }

  @Test
  @DisplayName("연습 대화의 힌트는 이야기 퀘스트 대신 상황의 다음 목표로 만든다")
  void hint_usesTheNextScenarioGoal() {
    ConversationStartResponse started =
        conversationService.startPractice(userId, SCENARIO, UUID.randomUUID(), 1.1);
    given(aiGateway.hint(any(), any(), any(), anyString(), anyInt()))
        .willReturn(
            new HintResult(
                List.of(new HintResult.Phrase("My room is too noisy.", "방이 너무 시끄러워요.")),
                List.of(),
                List.of()));

    turnHelpService.hint(userId, started.openingTurn().turnId(), UUID.randomUUID());

    then(aiGateway)
        .should(atLeastOnce())
        .hint(
            any(),
            any(),
            any(),
            argThat(
                goal ->
                    goal.contains("what is wrong with their room")
                        && !goal.contains("reservation")),
            anyInt());
  }

  private TurnResponse say(Long conversationId, String text, String... recognizedGoals) {
    given(aiGateway.judge(any(), any(), any(), any(), anyString()))
        .willReturn(
            new JudgeResult(
                text,
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent(
                    recognizedGoals.length == 0 ? "none" : recognizedGoals[0],
                    List.of(recognizedGoals),
                    null),
                List.of(),
                5));
    return turnService.send(
        userId, conversationId, new TurnRequest(UUID.randomUUID(), InputType.TEXT, text, null));
  }

  private PracticeProgressResponse progress(ConversationStartResponse started) {
    return placePractice.progress(userId, SCENARIO, started.conversationId());
  }
}
