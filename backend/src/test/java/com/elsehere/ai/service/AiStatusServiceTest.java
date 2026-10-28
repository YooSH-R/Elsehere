package com.elsehere.ai.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.dto.AiStatus;
import com.elsehere.ai.dto.AiStatusResponse;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.service.GuestConversationLimit;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.guest.conversation-limit=" + AiStatusServiceTest.GUEST_CONVERSATION_LIMIT
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  AiStatusService.class,
  GuestConversationLimit.class
})
class AiStatusServiceTest {

  static final int GUEST_CONVERSATION_LIMIT = 2;
  private static final Instant NOW = Instant.parse("2026-10-28T01:00:00Z");
  private static final Long NPC_ID = 1L;

  @Autowired private EntityManager entityManager;

  @Autowired private AiStatusService aiStatusService;

  @Test
  @DisplayName("가입 사용자는 게스트 한도보다 많이 대화했어도 AI를 쓸 수 있다")
  void member_isAvailableBeyondGuestLimit() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    startConversations(userId, GUEST_CONVERSATION_LIMIT + 1);

    AiStatusResponse response = aiStatusService.status(userId);

    assertThat(response.status()).isEqualTo(AiStatus.AI_AVAILABLE);
    assertThat(response.capabilities().canStartConversation()).isTrue();
    assertThat(response.capabilities().canUseSpeech()).isTrue();
    assertThat(response.capabilities().canGenerateFeedback()).isTrue();
  }

  @Test
  @DisplayName("게스트가 아직 대화 횟수를 다 쓰지 않았으면 AI를 쓸 수 있다")
  void guestBelowLimit_isAvailable() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1");
    startConversations(userId, GUEST_CONVERSATION_LIMIT - 1);

    AiStatusResponse response = aiStatusService.status(userId);

    assertThat(response.status()).isEqualTo(AiStatus.AI_AVAILABLE);
    assertThat(response.capabilities().canStartConversation()).isTrue();
  }

  @Test
  @DisplayName("게스트가 대화 횟수를 다 쓰면 소진 상태이고 새 대화만 시작할 수 없다")
  void guestReachedLimit_isExhaustedAndCannotStartConversation() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1");
    startConversations(userId, GUEST_CONVERSATION_LIMIT).get(0).endByUser(NOW);

    AiStatusResponse response = aiStatusService.status(userId);

    assertThat(response.status()).isEqualTo(AiStatus.AI_EXHAUSTED);
    assertThat(response.capabilities().canStartConversation()).isFalse();
    // 게스트 한도는 대화 시작만 막는다. 마지막 대화의 음성 입력과 피드백은 그대로 쓸 수 있다.
    assertThat(response.capabilities().canUseSpeech()).isTrue();
    assertThat(response.capabilities().canGenerateFeedback()).isTrue();
  }

  @Test
  @DisplayName("사용자가 DB에 없으면 NOT_FOUND로 거절한다")
  void missingUser_throwsNotFound() {
    assertThatThrownBy(() -> aiStatusService.status(999L))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.NOT_FOUND);
  }

  // 대화 수만 세므로 NPC 시드 없이 대화만 저장한다.
  private List<Conversation> startConversations(Long userId, int count) {
    List<Conversation> conversations = new ArrayList<>();
    for (int i = 0; i < count; i++) {
      Conversation conversation =
          Conversation.start(userId, NPC_ID, InitiatedBy.NPC, 1, NOW.plusSeconds(i));
      entityManager.persist(conversation);
      conversations.add(conversation);
    }
    return conversations;
  }
}
