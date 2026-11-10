package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
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

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.HintResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.review.service.ReviewCardService;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

@DialogueServiceTest
@Import(ReviewCardService.class)
@MockitoBean(types = {MemoryService.class, FollowUpService.class})
class ReviewConversationServiceTest {
  @Autowired private ConversationService conversationService;
  @Autowired private TurnService turnService;
  @Autowired private TurnHelpService turnHelpService;
  @Autowired private ConversationHistoryService historyService;
  @Autowired private ReviewCardService reviewCardService;
  @Autowired private EntityManager entityManager;
  @Autowired private AiGateway aiGateway;
  @Autowired private MemoryService memoryService;
  @Autowired private FollowUpService followUpService;
  @Autowired private FirstDayMissionRepository firstDayMissionRepository;
  private Long userId;
  private Long cardId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "review-dialogue-owner");
    Conversation source = conversation(entityManager, userId, "HOTEL_STAFF");
    source.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
    var feedback = ConversationFeedback.of(source.getId());
    feedback.complete(
        "호텔에서 짐을 맡기고 체크인 시간을 물었어요.",
        """
        {"situationExpressions":[
          {"expression":"Could you hold my luggage?","meaningKo":"짐을 맡아 주실 수 있나요?"},
          {"expression":"What time can I check in?","meaningKo":"언제 체크인할 수 있나요?"}
        ]}
        """,
        "fixture",
        null);
    entityManager.persist(feedback);
    cardId = reviewCardService.create(userId, source.getId()).id();
    FirstDayMission current = FirstDayMission.start(userId);
    current.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    firstDayMissionRepository.saveAndFlush(current);
  }

  @Test
  @DisplayName("복습 대화의 첫인사도 학습자가 고른 NPC 말 빠르기로 말한다")
  void reviewGreetingUsesTheChosenSpeechSpeed() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome!", null));

    conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.3);

    ArgumentCaptor<WorkerLearner> learner = ArgumentCaptor.forClass(WorkerLearner.class);
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), learner.capture());
    assertThat(learner.getValue().speechSpeed()).isEqualTo(1.3);
  }

  @Test
  void reviewRequestIdCannotStartANormalConversation() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome!", null));
    UUID requestId = UUID.randomUUID();
    conversationService.startReview(userId, cardId, requestId, 1.1);
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
  void cardDeletedWhileGreetingIsPendingDoesNotSaveAConversation() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              reviewCardService.delete(userId, cardId);
              entityManager.flush();
              return reply("Welcome!", null);
            });
    assertThatThrownBy(
            () -> conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(conversationService.active(userId)).isEmpty();
  }

  @Test
  void modeSurvivesReplayActiveDetailAndEnd() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome!", null));
    UUID requestId = UUID.randomUUID();
    var started = conversationService.startReview(userId, cardId, requestId, 1.1);
    assertThat(started.reviewCardId()).isEqualTo(cardId);
    assertThat(conversationService.startReview(userId, cardId, requestId, 1.1)).isEqualTo(started);
    assertThat(conversationService.active(userId).orElseThrow().reviewCardId()).isEqualTo(cardId);
    assertThat(conversationService.detail(userId, started.conversationId()).reviewCardId())
        .isEqualTo(cardId);
    var ended =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), false));
    assertThat(ended.reviewCardId()).isEqualTo(cardId);
    assertThat(ended.feedback()).isNull();
    assertThat(
            conversationService.end(
                userId,
                started.conversationId(),
                new EndConversationRequest(UUID.randomUUID(), false)))
        .isEqualTo(ended);
  }

  @Test
  void existingActiveConversationIsNotClosedForReview() {
    Conversation normal = conversation(entityManager, userId, "CAFE_STAFF");
    assertThatThrownBy(
            () -> conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error ->
                assertThat(error.getErrorCode()).isEqualTo(ErrorCode.CONVERSATION_ALREADY_ACTIVE));
    assertThat(normal.isActive()).isTrue();
    then(aiGateway).shouldHaveNoInteractions();
  }

  @Test
  void anotherUsersCardCannotStartReview() {
    Long other =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "review-other", "Other");
    assertThatThrownBy(() -> conversationService.startReview(other, cardId, UUID.randomUUID(), 1.1))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    then(aiGateway).shouldHaveNoInteractions();
  }

  @Test
  void hintUsesSavedSituationInsteadOfCurrentQuest() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome!", null));
    var started = conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);
    given(aiGateway.hint(any(), any(), any(), anyString(), anyInt()))
        .willReturn(
            new HintResult(
                List.of(new HintResult.Phrase("Could you hold my luggage?", "짐을 맡아 주실 수 있나요?")),
                List.of(),
                List.of()));
    var hint = turnHelpService.hint(userId, started.openingTurn().turnId(), UUID.randomUUID());
    assertThat(hint.hintText()).isEqualTo("Could you hold my luggage?");
    then(aiGateway)
        .should()
        .hint(
            any(),
            any(),
            any(),
            argThat(goal -> goal.contains("luggage") && !goal.contains("Order a drink")),
            anyInt());
  }

  @Test
  void threeRudeReviewTurnsCloseOnlyThePracticeWithoutAffinityPenalty() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Please be polite.", null));
    given(
            aiGateway.replyReacting(
                any(),
                any(),
                any(),
                any(),
                anyString(),
                anyString(),
                anyInt(),
                any(),
                any(),
                anyString()))
        .willReturn(reply("Please be polite.", null));
    var started = conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);
    given(aiGateway.guard(any(), any(), any(), anyString()))
        .willReturn(new GuardResult(new GuardResult.Guard(true, null), -20, List.of()));
    given(aiGateway.judge(any(), any(), any(), any(), anyString()))
        .willReturn(new JudgeResult("Go away!", false, null, null, List.of()));
    for (int count = 1; count <= 3; count++) {
      var turn =
          turnService.send(
              userId,
              started.conversationId(),
              new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Go away!", null));
      assertThat(turn.conversation().rudeCount()).isEqualTo(count);
      assertThat(turn.conversation().affinity()).isZero();
      assertThat(turn.policeDispatch()).isNull();
      assertThat(turn.conversation().status())
          .isEqualTo(count == 3 ? ConversationStatus.ENDED_BY_NPC : ConversationStatus.ACTIVE);
      if (count == 3) {
        assertThat(turn.conversation().endReason()).isEqualTo(ConversationEndReason.RUDE);
      }
    }
    assertThat(firstDayMissionRepository.findById(userId).orElseThrow().getStage()).isEqualTo(1);
    then(memoryService).shouldHaveNoInteractions();
  }

  @Test
  void reviewIsNotListedAsAnUnreviewedMainConversation() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome!", null));
    var started = conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);
    assertThat(historyService.list(userId, null, null, 20).items())
        .noneMatch(item -> item.conversationId().equals(started.conversationId()));
  }

  @Test
  void activeReviewCannotBeDeleted() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome!", null));
    conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);

    assertThatThrownBy(() -> reviewCardService.delete(userId, cardId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.REVIEW_IN_PROGRESS));
    assertThat(reviewCardService.detail(userId, cardId)).isNotNull();
  }

  @Test
  void reviewTurnKeepsMainQuestAndAffinityUnchanged() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Of course. How can I help?", null));
    var started = conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);
    given(aiGateway.guard(any(), any(), any(), anyString()))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    given(aiGateway.judge(any(), any(), any(), any(), anyString()))
        .willReturn(
            new JudgeResult(
                "Could you hold my luggage?",
                false,
                null,
                null,
                List.of(),
                new JudgeResult.Intent("order_drink", List.of("order_drink"), null),
                List.of("THANKS"),
                5));

    var turn =
        turnService.send(
            userId,
            started.conversationId(),
            new TurnRequest(UUID.randomUUID(), InputType.TEXT, "Could you hold my luggage?", null));

    assertThat(turn.conversation().affinity()).isZero();
    assertThat(firstDayMissionRepository.findById(userId).orElseThrow().getStage()).isEqualTo(1);
    then(memoryService).shouldHaveNoInteractions();
    then(aiGateway)
        .should()
        .reply(
            any(),
            any(),
            isNull(),
            any(),
            org.mockito.ArgumentMatchers.eq("Could you hold my luggage?"),
            argThat(
                instruction ->
                    instruction.contains("role-play") && !instruction.contains("Offer drinks")),
            anyInt(),
            any());
  }

  @Test
  void endingReviewDoesNotGenerateFeedbackMemoryOrFollowUp() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome! How can I help you?", null));
    given(followUpService.afterConversation(any(), any(), any()))
        .willReturn(CompletableFuture.completedFuture(Optional.empty()));
    var started = conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);

    var ended =
        conversationService.end(
            userId, started.conversationId(), new EndConversationRequest(UUID.randomUUID(), true));

    assertThat(ended.feedback()).isNull();
    assertThat(ended.followUp()).isNull();
    then(memoryService).shouldHaveNoInteractions();
    then(followUpService).shouldHaveNoInteractions();
    assertThat(firstDayMissionRepository.findById(userId).orElseThrow().getStage()).isEqualTo(1);
  }

  @Test
  void reviewOpensTheOriginalSituationWithoutCurrentQuestMemoryOrFollowUp() {
    given(aiGateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome! How can I help you?", null));

    var started = conversationService.startReview(userId, cardId, UUID.randomUUID(), 1.1);

    assertThat(started).isNotNull();
    assertThat(started.npc().npcCode()).isEqualTo("HOTEL_STAFF");
    assertThat(started.runningMissions()).isEmpty();
    assertThat(started.deliveredFollowUpId()).isNull();
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
                    instruction.contains("role-play")
                        && instruction.contains("호텔에서 짐을 맡기고 체크인 시간을 물었어요.")
                        && !instruction.contains("Offer drinks")
                        && !instruction.contains("Corner Cafe")),
            anyInt(),
            any());
    then(memoryService).shouldHaveNoInteractions();
    then(followUpService).shouldHaveNoInteractions();
    assertThat(firstDayMissionRepository.findById(userId).orElseThrow().getStage()).isEqualTo(1);
  }
}
