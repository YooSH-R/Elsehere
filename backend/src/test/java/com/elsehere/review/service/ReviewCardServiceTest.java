package com.elsehere.review.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.verifyNoInteractions;

import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.dialogue.service.DialogueServiceTest;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.review.repository.ReviewCardRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.service.UserContentEraser;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;
import org.junit.jupiter.params.provider.NullSource;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;

@DialogueServiceTest
@Import(ReviewCardService.class)
class ReviewCardServiceTest {
  private static final String STORED_REVIEWS =
      """
      {"situationExpressions":[
        {"expression":"Can I get that to go?","meaningKo":"포장해 주실 수 있나요?",
         "usageKo":"음료를 가져갈 때","exampleEn":"Can I get that to go?","exampleKo":"포장해 주세요."},
        {"expression":"Could I get less ice?","meaningKo":"얼음을 조금만 넣어 주세요.",
         "usageKo":"얼음 양을 바꿀 때","exampleEn":"Could I get less ice?","exampleKo":"얼음을 줄여 주세요."}
      ]}
      """;

  @Autowired private ReviewCardService reviewCardService;
  @Autowired private EntityManager entityManager;
  @Autowired private ReviewCardRepository reviewCardRepository;
  @Autowired private UserContentEraser userContentEraser;
  @Autowired private AiGateway aiGateway;
  private Long userId;
  private Conversation source;

  @AfterEach
  void noAiCalls() {
    verifyNoInteractions(aiGateway);
  }

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "review-owner");
    source = conversation(entityManager, userId, "CAFE_STAFF");
    source.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
    ConversationFeedback feedback = ConversationFeedback.of(source.getId());
    feedback.complete("카페에서 음료를 주문하고 포장을 부탁했어요.", STORED_REVIEWS, "fixture", null);
    entityManager.persist(feedback);
    entityManager.flush();
  }

  @Test
  void createSnapshotsTheOwnedConversationAndItsStoredExpressions() {
    var saved = reviewCardService.create(userId, source.getId());

    assertThat(saved).isNotNull();
    assertThat(saved.id()).isPositive();
    assertThat(saved.sourceConversationId()).isEqualTo(source.getId());
    assertThat(saved.sceneCode()).isEqualTo("CAFE");
    assertThat(saved.npcId()).isEqualTo(source.getNpcId());
    assertThat(saved.npc().name()).isEqualTo("Mia");
    assertThat(saved.targets())
        .extracting(target -> target.expression())
        .containsExactly("Can I get that to go?", "Could I get less ice?");
    assertThat(saved.practiceCount()).isZero();
    assertThat(saved.lastPracticedAt()).isNull();
    assertThat(saved.usedExpressions()).isEmpty();
  }

  @Test
  void capturedBagScenarioKeepsItsFactsAfterTheOriginalStageChanges() {
    source.attachFirstDayStage(FirstDayStage.FIND_BAG.ordinal());
    source.recordFirstDayGoals(FirstDayStage.FIND_BAG.intentIds());
    var saved = reviewCardService.create(userId, source.getId());
    assertThat(saved.scenario().title()).isEqualTo("분실 가방 찾기");
    source.attachFirstDayStage(FirstDayStage.CAFE_BREAK.ordinal());
    entityManager.flush();
    entityManager.clear();
    var loaded = reviewCardService.detail(userId, saved.id());
    assertThat(loaded.scenario()).isEqualTo(saved.scenario());
    assertThat(String.join(" ", loaded.scenario().learnerNotes())).contains("여권", "책");
  }

  @Test
  void listUsesAnOwnedNewestFirstCursorWithoutRepeatingRows() {
    var first = reviewCardService.create(userId, source.getId());
    var hotel = reviewCardService.create(userId, endedConversation(userId, "HOTEL_STAFF").getId());
    var cafe = reviewCardService.create(userId, endedConversation(userId, "CAFE_STAFF").getId());
    Long otherUser =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "other-review-owner", "Other");
    reviewCardService.create(otherUser, endedConversation(otherUser, "CAFE_STAFF").getId());
    entityManager.flush();
    entityManager.clear();

    var page = reviewCardService.list(userId, null, null, 2);

    assertThat(page).isNotNull();
    assertThat(page.items()).extracting(card -> card.id()).containsExactly(cafe.id(), hotel.id());
    assertThat(page.nextCursor()).isEqualTo(hotel.id().toString());
    var next = reviewCardService.list(userId, null, page.nextCursor(), 2);
    assertThat(next.items()).extracting(card -> card.id()).containsExactly(first.id());
    assertThat(next.nextCursor()).isNull();
    assertThat(reviewCardService.list(userId, "CAFE", null, 20).items())
        .extracting(card -> card.id())
        .containsExactly(cafe.id(), first.id());
  }

  @Test
  void onlyTheOwnerCanReadOrDeleteTheSavedCard() {
    var saved = reviewCardService.create(userId, source.getId());
    Long otherUser =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "detail-other", "Other");
    entityManager.flush();
    entityManager.clear();

    assertThat(reviewCardService.detail(userId, saved.id())).isEqualTo(saved);
    assertThatThrownBy(() -> reviewCardService.detail(otherUser, saved.id()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThatThrownBy(() -> reviewCardService.delete(otherUser, saved.id()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(reviewCardService.detail(userId, saved.id())).isEqualTo(saved);
    reviewCardService.delete(userId, saved.id());
    entityManager.flush();
    entityManager.clear();
    assertThatThrownBy(() -> reviewCardService.detail(userId, saved.id()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  void duplicateCreateReturnsTheOriginalSnapshotEvenIfFeedbackChanges() {
    var original = reviewCardService.create(userId, source.getId());
    entityManager
        .find(ConversationFeedback.class, source.getId())
        .complete("다시 만든 다른 피드백", "{}", "fixture", null);
    entityManager.flush();
    entityManager.clear();

    assertThat(reviewCardService.create(userId, source.getId())).isEqualTo(original);
    assertThat(reviewCardRepository.count()).isEqualTo(1);
  }

  @Test
  void anotherUsersSourceIsNotFoundAndCreatesNoCard() {
    assertThatThrownBy(() -> reviewCardService.create(userId + 1000, source.getId()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(reviewCardRepository.count()).isZero();
  }

  @ParameterizedTest
  @NullSource
  @ValueSource(
      strings = {
        "{}",
        "{\"situationExpressions\":[]}",
        "{\"situationExpressions\":[{\"expression\":\"hello\",\"meaningKo\":\"안녕\"}]}"
      })
  void insufficientStoredExpressionsNeverTriggerGeneration(String reviews) {
    entityManager
        .find(ConversationFeedback.class, source.getId())
        .complete("상황", reviews, "fixture", null);

    assertNotReady(source.getId());
  }

  @ParameterizedTest
  @EnumSource(
      value = FeedbackStatus.class,
      names = {"FAILED", "SKIPPED"})
  void unfinishedFeedbackCannotCreateAReviewCard(FeedbackStatus status) {
    var feedback = entityManager.find(ConversationFeedback.class, source.getId());
    if (status == FeedbackStatus.FAILED) {
      feedback.fail("fixture", null);
    } else {
      feedback.skip(null);
    }
    assertNotReady(source.getId());
  }

  @Test
  void activeConversationCannotBeSavedAsAReviewCard() {
    Conversation active = conversation(entityManager, userId, "HOTEL_STAFF");
    var feedback = ConversationFeedback.of(active.getId());
    feedback.complete("아직 대화 중", STORED_REVIEWS, "fixture", null);
    entityManager.persist(feedback);
    entityManager.flush();

    assertNotReady(active.getId());
  }

  @ParameterizedTest
  @ValueSource(strings = {"", "0", "-1", "not-an-id", "9223372036854775808"})
  void invalidCursorIsAValidationFailure(String cursor) {
    assertThatThrownBy(() -> reviewCardService.list(userId, null, cursor, 20))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
  }

  @ParameterizedTest
  @ValueSource(ints = {0, 51})
  void invalidPageSizeIsAValidationFailure(int size) {
    assertThatThrownBy(() -> reviewCardService.list(userId, null, null, size))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
  }

  @Test
  void erasingUserContentAlsoErasesSavedReviewMaterial() {
    var card = reviewCardService.create(userId, source.getId());

    userContentEraser.deleteContent(userId);
    entityManager.clear();

    assertThat(reviewCardRepository.findById(card.id())).isEmpty();
  }

  private void assertNotReady(Long conversationId) {
    assertThatThrownBy(() -> reviewCardService.create(userId, conversationId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.REVIEW_NOT_READY));
    assertThat(reviewCardRepository.count()).isZero();
  }

  private Conversation endedConversation(Long owner, String npcCode) {
    Conversation conversation = conversation(entityManager, owner, npcCode);
    conversation.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
    ConversationFeedback feedback = ConversationFeedback.of(conversation.getId());
    feedback.complete("상황을 다시 연습해요.", STORED_REVIEWS, "fixture", null);
    entityManager.persist(feedback);
    entityManager.flush();
    return conversation;
  }
}
