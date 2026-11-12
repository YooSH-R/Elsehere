package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.review.service.ReviewCardService;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

@DialogueServiceTest
@MockitoBean(types = {MemoryService.class, FollowUpService.class})
class ReviewPracticeResultTest {
  @Autowired private ConversationService service;
  @Autowired private ReviewCardService cards;
  @Autowired private com.elsehere.user.service.UserContentEraser eraser;
  @Autowired private com.elsehere.review.service.ReviewPracticeResults results;
  @Autowired private EntityManager em;
  @Autowired private AiGateway gateway;
  private Long userId;
  private Long cardId;
  private Conversation review;

  @Test
  void lastSpokenFarewellIsIncludedWhenPracticeEndsImmediately() {
    var original = cards.detail(userId, cardId);
    var source = conversation(em, userId, "HOTEL_STAFF");
    source.endByUser(review.getStartedAt());
    var card =
        com.elsehere.review.entity.ReviewCard.of(
            userId,
            source.getId(),
            original.npcId(),
            "CAFE",
            "분실물 찾기",
            """
        [{"expression":"Has anyone turned in a blue bag?","meaningKo":"파란 가방 맡긴 것 있나요?"},
         {"expression":"That's the one!","meaningKo":"바로 그거예요!"},
         {"expression":"You're a lifesaver!","meaningKo":"정말 큰 도움이 됐어요!"}]
        """);
    em.persist(card);
    review.attachReviewCard(card.getId());
    userTurn(1, "Has anyone turned in a blue bag?", false);
    userTurn(3, "That's the one!", false);
    assertThat(results.progress(userId, card.getId(), review.getId()).verifiedExpressionCount())
        .isEqualTo(2);
    Turn last = userTurn(5, "Thank you. You are a lifesaver!", false);
    var ended =
        service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    assertThat(ended.reviewResult().verifiedExpressionCount()).isEqualTo(3);
    assertThat(ended.reviewResult().expressions().getLast().evidence())
        .extracting("turnId")
        .containsExactly(last.getId());
    assertThat(results.latest(userId, card.getId()).expressions())
        .isEqualTo(ended.reviewResult().expressions());
    org.mockito.BDDMockito.then(gateway).shouldHaveNoInteractions();
  }

  @Test
  void progressRejectsOtherOwnersAndUnrelatedNormalConversations() {
    Long other = UserTestData.insertUser(em, AuthProvider.GOOGLE, "progress-other", "Other");
    assertThatThrownBy(() -> results.progress(other, cardId, review.getId()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    Long sourceId = cards.detail(userId, cardId).sourceConversationId();
    assertThatThrownBy(() -> results.progress(userId, cardId, sourceId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  void liveProgressReadsAcceptedEvidenceWithoutEndingOrRecordingPractice() {
    Turn first = userTurn(1, "Could you please hold my luggage?", false);
    em.persist(Turn.npc(review, 2, "What time can I check in?", null, review.getStartedAt()));
    userTurn(3, "What time can I check in?", true);
    var progress = results.progress(userId, cardId, review.getId());
    assertThat(progress.verifiedExpressionCount()).isEqualTo(1);
    assertThat(progress.expressions().getFirst().evidence())
        .extracting("turnId")
        .containsExactly(first.getId());
    assertThat(progress.expressions().get(1).evidence()).isEmpty();
    assertThat(cards.detail(userId, cardId).practiceCount()).isZero();
    assertThat(review.getReviewResult()).isNull();
    assertThat(review.getEndedAt()).isNull();
    assertThat(results.progress(userId, cardId, review.getId())).isEqualTo(progress);
    userTurn(4, "What time can I check in?", null);
    var updated = results.progress(userId, cardId, review.getId());
    assertThat(updated.verifiedExpressionCount()).isEqualTo(2);
    var ended =
        service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    assertThat(ended.reviewResult().expressions()).isEqualTo(updated.expressions());
    org.mockito.BDDMockito.then(gateway).shouldHaveNoInteractions();
  }

  @BeforeEach
  void prepareReview() {
    userId = UserTestData.insertUser(em, AuthProvider.GOOGLE, "review-result-owner");
    Conversation source = conversation(em, userId, "HOTEL_STAFF");
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
    em.persist(feedback);
    var card = cards.create(userId, source.getId());
    cardId = card.id();
    review = Conversation.start(userId, card.npcId(), InitiatedBy.USER, 1, source.getStartedAt());
    review.attachReviewCard(cardId);
    em.persist(review);
  }

  @Test
  void equivalentPoliteVariantsDoNotCountAsTwoDifferentExpressions() {
    var original = cards.detail(userId, cardId);
    var source = conversation(em, userId, "HOTEL_STAFF");
    source.endByUser(review.getStartedAt());
    var variantCard =
        com.elsehere.review.entity.ReviewCard.of(
            userId,
            source.getId(),
            original.npcId(),
            "HOTEL",
            "짐 맡기기",
            """
        [{"expression":"Can you hold my luggage?","meaningKo":"짐을 맡아 주실 수 있나요?"},
         {"expression":"Could you please hold my luggage?","meaningKo":"짐을 맡아 주실 수 있나요?"}]
        """);
    em.persist(variantCard);
    review.attachReviewCard(variantCard.getId());
    userTurn(1, "Could you please hold my luggage?", false);
    var result =
        service
            .end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false))
            .reviewResult();
    assertThat(result.verifiedExpressionCount()).isEqualTo(1);
    assertThat(result.practiceCompleted()).isFalse();
  }

  @Test
  void legacyStaffTargetIsHiddenAndExactLearnerSentencesAppearInProgressAndFinalResult() {
    var original = cards.detail(userId, cardId);
    var source = conversation(em, userId, "HOTEL_STAFF");
    source.endByUser(review.getStartedAt());
    var legacy =
        com.elsehere.review.entity.ReviewCard.of(
            userId,
            source.getId(),
            original.npcId(),
            "HOTEL",
            "상황 복습",
            """
        [{"expression":"You're all set.","meaningKo":"다 됐어요."},
         {"expression":"No, thanks. I'm good.","meaningKo":"아니요, 괜찮아요."},
         {"expression":"That's the one!","meaningKo":"바로 그거예요!"}]
        """);
    em.persist(legacy);
    review.attachReviewCard(legacy.getId());
    Turn first = userTurn(1, "No, thanks. I'm good.", false);
    userTurn(2, "That's the one!", null);
    var progress = results.progress(userId, legacy.getId(), review.getId());
    assertThat(cards.detail(userId, legacy.getId()).targets()).hasSize(2);
    assertThat(progress.verifiedExpressionCount()).isEqualTo(2);
    assertThat(progress.expressions().getFirst().evidence())
        .extracting("turnId")
        .containsExactly(first.getId());
    var ended =
        service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    assertThat(ended.reviewResult().verifiedExpressionCount())
        .isEqualTo(progress.verifiedExpressionCount());
    assertThat(ended.reviewResult().practiceCompleted()).isTrue();
    assertThat(ended.reviewResult().expressions()).hasSize(2);
    org.mockito.BDDMockito.then(gateway).shouldHaveNoInteractions();
  }

  @Test
  void contentErasureAlsoClearsUtterancesCachedOnRetainedConversationRows() {
    userTurn(1, "Could you hold my luggage?", false);
    service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    Long conversationId = review.getId();
    em.flush();
    em.clear();

    eraser.deleteContent(userId);
    em.clear();

    assertThat(em.find(Conversation.class, conversationId).getReviewResult()).isNull();
  }

  @Test
  void newestResultUsesEndTimeThenIdAndDoesNotOverwriteOlderEvidence() {
    userTurn(1, "Could you hold my luggage?", false);
    var first =
        service
            .end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false))
            .reviewResult();
    var card = cards.detail(userId, cardId);
    Conversation next =
        Conversation.start(userId, card.npcId(), InitiatedBy.USER, 1, review.getStartedAt());
    next.attachReviewCard(cardId);
    em.persist(next);
    em.persist(
        Turn.user(
            next,
            1,
            UUID.randomUUID(),
            InputType.TEXT,
            "What time can I check in?",
            null,
            null,
            false,
            null,
            null));
    var second =
        service
            .end(userId, next.getId(), new EndConversationRequest(UUID.randomUUID(), false))
            .reviewResult();
    assertThat(results.latest(userId, cardId)).isEqualTo(second);
    assertThat(results.detail(userId, cardId, review.getId())).isEqualTo(first);
    assertThat(cards.detail(userId, cardId).practiceCount()).isEqualTo(2);
    assertThat(cards.detail(userId, cardId).usedExpressions())
        .containsExactly("What time can I check in?");
  }

  @Test
  void aDifferentCardCannotReadThisAttemptsResult() {
    service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    Conversation source = conversation(em, userId, "HOTEL_STAFF");
    source.endByUser(review.getStartedAt());
    var feedback = ConversationFeedback.of(source.getId());
    feedback.complete(
        "다른 상황",
        """
        {"situationExpressions":[
          {"expression":"How are you?","meaningKo":"어떻게 지내요?"},
          {"expression":"Have a good one!","meaningKo":"좋은 하루 보내요!"}
        ]}
        """,
        "fixture",
        null);
    em.persist(feedback);
    Long otherCard = cards.create(userId, source.getId()).id();
    assertThatThrownBy(() -> results.detail(userId, otherCard, review.getId()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  void blockedNpcAndMachineTranslatedLinesAreNotUsageEvidence() {
    userTurn(1, "Could you hold my luggage?", true);
    em.persist(Turn.npc(review, 2, "What time can I check in?", null, review.getStartedAt()));
    Turn machineTranslated =
        Turn.user(
            review,
            3,
            UUID.randomUUID(),
            InputType.TEXT,
            "언제 체크인하나요?",
            null,
            "What time can I check in?",
            false,
            null,
            null);
    em.persist(machineTranslated);
    Turn actual = userTurn(4, "Could you hold my luggage?", null);
    var result =
        service
            .end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false))
            .reviewResult();
    assertThat(result.practiceCompleted()).isFalse();
    assertThat(result.verifiedExpressionCount()).isEqualTo(1);
    assertThat(result.expressions().getFirst().evidence())
        .extracting("turnId")
        .containsExactly(actual.getId());
    assertThat(result.expressions().get(1).evidence()).isEmpty();
    assertThat(result.utterances())
        .extracting("turnId")
        .containsExactly(machineTranslated.getId(), actual.getId());
  }

  @Test
  void notFindingTargetsIsNotAnErrorAndOtherSpeechRemainsAvailable() {
    userTurn(1, "Hello, I would like to ask something.", false);
    var result =
        service
            .end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false))
            .reviewResult();
    assertThat(result.practiceCompleted()).isFalse();
    assertThat(result.verifiedExpressionCount()).isZero();
    assertThat(result.utterances()).hasSize(1);
    assertThat(result.expressions()).allSatisfy(item -> assertThat(item.evidence()).isEmpty());
    assertThat(cards.detail(userId, cardId).practiceCount()).isEqualTo(1);
  }

  @Test
  void unfinishedMissingAndOtherOwnersResultsAreNotReadable() {
    assertThatThrownBy(() -> results.latest(userId, cardId)).isInstanceOf(BusinessException.class);
    service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    Long other = UserTestData.insertUser(em, AuthProvider.GOOGLE, "review-results-other", "Other");
    assertThatThrownBy(() -> results.detail(other, cardId, review.getId()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThatThrownBy(() -> results.detail(userId, cardId, -1L))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  void savedResultCanBeReopenedWithoutAi() {
    userTurn(1, "Could you hold my luggage?", false);
    var ended =
        service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));
    assertThat(results.latest(userId, cardId)).isEqualTo(ended.reviewResult());
    assertThat(results.detail(userId, cardId, review.getId())).isEqualTo(ended.reviewResult());
    org.mockito.BDDMockito.then(gateway).shouldHaveNoInteractions();
  }

  @Test
  void endRecordsTwoExpressionEvidenceOnceAndKeepsActualUtterances() {
    Turn first = userTurn(1, "Could you please hold my luggage?", false);
    Turn second = userTurn(2, "What time can I check in?", null);

    var ended =
        service.end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false));

    assertThat(ended.reviewResult()).isNotNull();
    assertThat(ended.reviewResult().practiceCompleted()).isTrue();
    assertThat(ended.reviewResult().verifiedExpressionCount()).isEqualTo(2);
    assertThat(ended.reviewResult().utterances())
        .extracting("turnId")
        .containsExactly(first.getId(), second.getId());
    assertThat(ended.reviewResult().expressions().getFirst().evidence().getFirst().text())
        .isEqualTo(first.getText());
    assertThat(cards.detail(userId, cardId).practiceCount()).isEqualTo(1);
    assertThat(
            service
                .end(userId, review.getId(), new EndConversationRequest(UUID.randomUUID(), false))
                .reviewResult())
        .isEqualTo(ended.reviewResult());
    assertThat(cards.detail(userId, cardId).practiceCount()).isEqualTo(1);
    org.mockito.BDDMockito.then(gateway).shouldHaveNoInteractions();
  }

  private Turn userTurn(int seq, String text, Boolean rude) {
    Turn turn =
        Turn.user(
            review, seq, UUID.randomUUID(), InputType.TEXT, text, null, null, rude, null, null);
    em.persist(turn);
    return turn;
  }
}
