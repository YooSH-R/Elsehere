package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.dialogue.dto.ConversationListResponse;
import com.elsehere.dialogue.dto.ConversationSummaryResponse;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.review.entity.ReviewCard;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.entity.PlaceGroup;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.time.OffsetDateTime;
import org.assertj.core.api.ThrowableAssert.ThrowingCallable;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;

// 지난 대화 목록(CONV-09). 저장된 대화만 조회하므로 AI 워커를 부르지 않는다.
@DialogueDbTest
class ConversationHistoryServiceTest {

  private static final Instant MORNING = Instant.parse("2026-10-21T01:00:00Z");

  @Autowired private ConversationHistoryService conversationHistoryService;

  @Autowired private EntityManager entityManager;

  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
  }

  @Test
  @DisplayName("내 대화만 최근 시작 순으로 NPC·장소·상태·발화 수·피드백 상태와 함께 돌려준다")
  void list_returnsMyConversationsRecentFirst() {
    Conversation cafe = start(userId, "CAFE_STAFF", MORNING);
    npcTurn(entityManager, cafe, 1, "Hi! What can I get for you?");
    textTurn(entityManager, cafe, 2, "I want a latte.");
    npcTurn(entityManager, cafe, 3, "Sure!");
    textTurn(entityManager, cafe, 4, "Thank you.");
    cafe.endByUser(MORNING.plusSeconds(300));
    ConversationFeedback feedback = ConversationFeedback.of(cafe.getId());
    feedback.complete("라테를 주문했어요.", "{}", "test-model", null);
    entityManager.persist(feedback);
    Conversation hotel = start(userId, "HOTEL_STAFF", MORNING.plusSeconds(3600));
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    start(otherUserId, "CAFE_STAFF", MORNING.plusSeconds(7200));
    entityManager.flush();

    ConversationListResponse response = conversationHistoryService.list(userId, null, null, 20);

    assertThat(response.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(hotel.getId(), cafe.getId());
    assertThat(response.nextCursor()).isNull();
    ConversationSummaryResponse ended = response.items().get(1);
    assertThat(ended.npc().npcCode()).isEqualTo("CAFE_STAFF");
    assertThat(ended.npc().name()).isEqualTo("Mia");
    assertThat(ended.sceneCode()).isEqualTo("CAFE");
    assertThat(ended.status()).isEqualTo(ConversationStatus.ENDED_BY_USER);
    assertThat(ended.endReason()).isEqualTo(ConversationEndReason.USER_ENDED);
    assertThat(ended.startedAt()).isEqualTo(OffsetDateTime.parse("2026-10-21T10:00:00+09:00"));
    assertThat(ended.endedAt()).isEqualTo(OffsetDateTime.parse("2026-10-21T10:05:00+09:00"));
    assertThat(ended.userTurnCount()).isEqualTo(2);
    assertThat(ended.feedbackStatus()).isEqualTo(FeedbackStatus.DONE);
    ConversationSummaryResponse active = response.items().get(0);
    assertThat(active.status()).isEqualTo(ConversationStatus.ACTIVE);
    assertThat(active.userTurnCount()).isZero();
    assertThat(active.feedbackStatus()).isNull();
  }

  @Test
  @DisplayName("크기만큼 나눠 주고, 마지막 항목의 대화 ID를 커서로 보내면 다음 페이지를 준다")
  void list_pagesWithCursor() {
    Conversation first = start(userId, "CAFE_STAFF", MORNING);
    Conversation second = start(userId, "CAFE_STAFF", MORNING.plusSeconds(60));
    Conversation third = start(userId, "CAFE_STAFF", MORNING.plusSeconds(120));
    entityManager.flush();

    ConversationListResponse firstPage = conversationHistoryService.list(userId, null, null, 2);
    ConversationListResponse lastPage =
        conversationHistoryService.list(userId, null, firstPage.nextCursor(), 2);

    assertThat(firstPage.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(third.getId(), second.getId());
    assertThat(firstPage.nextCursor()).isEqualTo(String.valueOf(second.getId()));
    assertThat(lastPage.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(first.getId());
    assertThat(lastPage.nextCursor()).isNull();
  }

  @Test
  @DisplayName("같은 시각에 시작한 대화는 나중에 만든 대화가 먼저이고, 커서로 빠짐없이 이어 받는다")
  void list_sameStartTime_ordersByNewestAndPagesWithoutGap() {
    Conversation earlier = start(userId, "CAFE_STAFF", MORNING);
    Conversation later = start(userId, "CAFE_STAFF", MORNING);
    entityManager.flush();

    ConversationListResponse firstPage = conversationHistoryService.list(userId, null, null, 1);
    ConversationListResponse lastPage =
        conversationHistoryService.list(userId, null, firstPage.nextCursor(), 1);

    assertThat(firstPage.items().get(0).conversationId()).isEqualTo(later.getId());
    assertThat(lastPage.items().get(0).conversationId()).isEqualTo(earlier.getId());
    assertThat(lastPage.nextCursor()).isNull();
  }

  @Test
  @DisplayName("NPC 코드를 주면 그 NPC와 나눈 대화만 돌려준다")
  void list_byNpc_returnsOnlyThatNpc() {
    start(userId, "CAFE_STAFF", MORNING);
    Conversation hotel = start(userId, "HOTEL_STAFF", MORNING.plusSeconds(60));
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, "HOTEL_STAFF", null, 20);

    assertThat(response.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(hotel.getId());
  }

  @Test
  @DisplayName("없는 NPC 코드면 빈 목록을 돌려준다")
  void list_unknownNpc_returnsEmpty() {
    start(userId, "CAFE_STAFF", MORNING);
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, "NO_SUCH_NPC", null, 20);

    assertThat(response.items()).isEmpty();
    assertThat(response.nextCursor()).isNull();
  }

  @Test
  @DisplayName("숫자가 아닌 커서는 VALIDATION_FAILED로 거절한다")
  void list_nonNumericCursor_isRejected() {
    assertValidationFailed(() -> conversationHistoryService.list(userId, null, "abc", 20));
  }

  @Test
  @DisplayName("다른 사용자의 대화를 가리키는 커서는 VALIDATION_FAILED로 거절한다")
  void list_otherUsersCursor_isRejected() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    Conversation others = start(otherUserId, "CAFE_STAFF", MORNING);
    entityManager.flush();

    assertValidationFailed(
        () -> conversationHistoryService.list(userId, null, String.valueOf(others.getId()), 20));
  }

  @ParameterizedTest
  @ValueSource(ints = {0, -1, 51})
  @DisplayName("크기가 1~50을 벗어나면 VALIDATION_FAILED로 거절한다")
  void list_sizeOutOfRange_isRejected(int size) {
    assertValidationFailed(() -> conversationHistoryService.list(userId, null, null, size));
  }

  @Test
  @DisplayName("피드백 목록에서는 내 완료된 피드백만 돌려주고 생성 전·건너뜀·실패는 제외한다")
  void list_feedbackOnly_returnsOnlyCompletedFeedback() {
    Conversation done = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(done);
    start(userId, "HOTEL_STAFF", MORNING.plusSeconds(60));
    Conversation skipped = start(userId, "CAFE_STAFF", MORNING.plusSeconds(120));
    ConversationFeedback skippedFeedback = ConversationFeedback.of(skipped.getId());
    skippedFeedback.skip(null);
    entityManager.persist(skippedFeedback);
    Conversation failed = start(userId, "CAFE_STAFF", MORNING.plusSeconds(180));
    ConversationFeedback failedFeedback = ConversationFeedback.of(failed.getId());
    failedFeedback.fail("UPSTREAM_ERROR", null);
    entityManager.persist(failedFeedback);
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    completeFeedback(start(otherUserId, "CAFE_STAFF", MORNING.plusSeconds(240)));
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, null, null, null, 20, true);

    assertThat(response.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(done.getId());
    assertThat(response.items())
        .extracting(ConversationSummaryResponse::feedbackStatus)
        .containsExactly(FeedbackStatus.DONE);
    assertThat(response.nextCursor()).isNull();
  }

  @Test
  @DisplayName("완료 피드백 필터를 페이지 크기 전에 적용하고 같은 시각의 커서를 빠짐없이 잇는다")
  void list_feedbackOnly_filtersBeforePagination() {
    Conversation oldest = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(oldest);
    Conversation middle = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(middle);
    Conversation newest = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(newest);
    for (int i = 0; i < 25; i++) {
      start(userId, "CAFE_STAFF", MORNING.plusSeconds(i + 1));
    }
    completeFeedback(start(userId, "HOTEL_STAFF", MORNING.plusSeconds(120)));
    entityManager.flush();

    ConversationListResponse first =
        conversationHistoryService.list(userId, "CAFE_STAFF", null, null, 2, true);
    ConversationListResponse last =
        conversationHistoryService.list(userId, "CAFE_STAFF", null, first.nextCursor(), 2, true);

    assertThat(first.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(newest.getId(), middle.getId());
    assertThat(last.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(oldest.getId());
    assertThat(last.nextCursor()).isNull();
    assertThat(first.items()).allMatch(ConversationSummaryResponse::feedbackUnread);
  }

  @Test
  @DisplayName("완료된 피드백이 없으면 불필요한 다음 페이지 없이 빈 목록을 돌려준다")
  void list_feedbackOnly_noCompletedFeedback_returnsEmptyWithoutCursor() {
    for (int i = 0; i < 25; i++) {
      start(userId, "CAFE_STAFF", MORNING.plusSeconds(i));
    }
    entityManager.flush();
    ConversationListResponse result =
        conversationHistoryService.list(userId, null, null, null, 2, true);
    assertThat(result.items()).isEmpty();
    assertThat(result.nextCursor()).isNull();
  }

  @Test
  @DisplayName("대화마다 그 대화로 다시 연습한 횟수를 돌려주고, 연습하지 않은 대화는 0이다")
  void list_feedbackOnly_returnsPracticeCountPerConversation() {
    Conversation practiced = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(practiced);
    Conversation fresh = start(userId, "CAFE_STAFF", MORNING.plusSeconds(60));
    completeFeedback(fresh);
    ReviewCard card =
        ReviewCard.of(userId, practiced.getId(), practiced.getNpcId(), "CAFE", "라테를 주문했어요.", "[]");
    card.recordPractice(MORNING.plusSeconds(900), "[]");
    card.recordPractice(MORNING.plusSeconds(1200), "[]");
    entityManager.persist(card);
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, null, null, null, 20, true);

    assertThat(response.items())
        .extracting(
            ConversationSummaryResponse::conversationId, ConversationSummaryResponse::practiceCount)
        .containsExactly(tuple(fresh.getId(), 0), tuple(practiced.getId(), 2));
  }

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  @DisplayName("장소 코드를 주면 그 장소의 NPC와 나눈 대화만 돌려준다")
  void list_byScene_returnsOnlyThatPlace(boolean feedbackOnly) {
    Conversation cafe = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(cafe);
    completeFeedback(start(userId, "HOTEL_STAFF", MORNING.plusSeconds(60)));
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, null, "CAFE", null, 20, feedbackOnly);

    assertThat(response.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(cafe.getId());
  }

  @ParameterizedTest
  @ValueSource(strings = {"", " "})
  @DisplayName("비어 있는 장소 코드는 VALIDATION_FAILED로 거절한다")
  void list_blankScene_isRejected(String sceneCode) {
    assertValidationFailed(
        () -> conversationHistoryService.list(userId, null, sceneCode, null, 20, true));
  }

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  @DisplayName("장소 묶음을 주면 NPC의 실제 근무지가 그 묶음인 대화만 돌려주고, 근무 시간 밖에 만난 대화는 뺀다")
  void list_byPlaceGroup_returnsConversationsAtThatWorkplaceGroup(boolean feedbackOnly) {
    Conversation cafe = start(userId, "CAFE_STAFF", MORNING);
    completeFeedback(cafe);
    // 소방대장 Ray는 마을 NPC지만 식당에서 일한다.
    Conversation diner = start(userId, "FIRE_CAPTAIN", MORNING.plusSeconds(60));
    completeFeedback(diner);
    completeFeedback(start(userId, "HOTEL_STAFF", MORNING.plusSeconds(120)));
    Conversation offDuty = start(userId, "CAFE_BARISTA", MORNING.plusSeconds(180));
    offDuty.markNpcOffDuty();
    completeFeedback(offDuty);
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, null, null, "FOOD", null, 20, feedbackOnly);

    assertThat(response.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(diner.getId(), cafe.getId());
  }

  @Test
  @DisplayName("일상 묶음에는 거리에서 만난 NPC와 근무 시간 밖에 만난 NPC와의 대화가 함께 들어간다")
  void list_dailyPlaceGroup_includesOffDutyConversations() {
    Conversation street = start(userId, "TOWN_PASSERBY", MORNING);
    completeFeedback(street);
    Conversation offDuty = start(userId, "CAFE_BARISTA", MORNING.plusSeconds(60));
    offDuty.markNpcOffDuty();
    completeFeedback(offDuty);
    completeFeedback(start(userId, "CAFE_STAFF", MORNING.plusSeconds(120)));
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, null, null, "DAILY", null, 20, true);

    assertThat(response.items())
        .extracting(ConversationSummaryResponse::conversationId)
        .containsExactly(offDuty.getId(), street.getId());
  }

  @Test
  @DisplayName("대화마다 NPC가 일하는 곳과 장소 묶음을 주고, 근무 시간 밖 대화는 장소 없이 일상이다")
  void list_returnsWorkplaceAndPlaceGroup() {
    Conversation diner = start(userId, "FIRE_CAPTAIN", MORNING);
    completeFeedback(diner);
    Conversation offDuty = start(userId, "CAFE_BARISTA", MORNING.plusSeconds(60));
    offDuty.markNpcOffDuty();
    completeFeedback(offDuty);
    entityManager.flush();

    ConversationListResponse response =
        conversationHistoryService.list(userId, null, null, null, null, 20, true);

    assertThat(response.items())
        .extracting(
            ConversationSummaryResponse::conversationId,
            ConversationSummaryResponse::placeName,
            ConversationSummaryResponse::placeGroup)
        .containsExactly(
            tuple(offDuty.getId(), null, PlaceGroup.DAILY),
            tuple(diner.getId(), "햄버거 가게", PlaceGroup.FOOD));
  }

  @ParameterizedTest
  @ValueSource(strings = {"", " ", "PARK", "food"})
  @DisplayName("없는 장소 묶음은 VALIDATION_FAILED로 거절한다")
  void list_unknownPlaceGroup_isRejected(String placeGroup) {
    assertValidationFailed(
        () -> conversationHistoryService.list(userId, null, null, placeGroup, null, 20, true));
  }

  private void completeFeedback(Conversation conversation) {
    conversation.endByUser(MORNING.plusSeconds(600));
    ConversationFeedback feedback = ConversationFeedback.of(conversation.getId());
    feedback.complete("대화를 마쳤어요.", "{}", "test-model", null);
    entityManager.persist(feedback);
  }

  private Conversation start(Long ownerId, String npcCode, Instant startedAt) {
    Conversation conversation =
        Conversation.start(ownerId, npcId(entityManager, npcCode), InitiatedBy.NPC, 1, startedAt);
    entityManager.persist(conversation);
    return conversation;
  }

  private static void assertValidationFailed(ThrowingCallable callable) {
    assertThatThrownBy(callable)
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.VALIDATION_FAILED);
  }
}
