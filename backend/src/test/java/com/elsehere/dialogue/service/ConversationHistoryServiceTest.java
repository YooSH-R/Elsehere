package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

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
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
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
@DialogueServiceTest
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
