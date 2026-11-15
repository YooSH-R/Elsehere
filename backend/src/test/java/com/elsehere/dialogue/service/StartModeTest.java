package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.review.dto.ReviewCardResponse;
import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class StartModeTest {

  private static final Instant NOW = Instant.parse("2030-01-10T00:00:00Z");

  @Test
  @DisplayName("같은 요청 ID로 저장된 대화는 같은 방식(일반·같은 복습 카드·같은 연습 상황)으로 시작했을 때만 같은 시작으로 본다")
  void matches_onlySameWayOfStarting() {
    Conversation story = conversation();
    Conversation reviewOfCard1 = conversation();
    reviewOfCard1.attachReviewCard(1L);
    Conversation dinerTable = conversation();
    dinerTable.attachPracticeScenario("DINER_TABLE");

    assertThat(StartMode.STORY.matches(story)).isTrue();
    assertThat(StartMode.STORY.matches(reviewOfCard1)).isFalse();
    assertThat(StartMode.STORY.matches(dinerTable)).isFalse();
    assertThat(StartMode.review(card(1L)).matches(reviewOfCard1)).isTrue();
    assertThat(StartMode.review(card(2L)).matches(reviewOfCard1)).isFalse();
    assertThat(StartMode.review(card(1L)).matches(story)).isFalse();
    assertThat(StartMode.practice(PracticeScenario.DINER_TABLE).matches(dinerTable)).isTrue();
    assertThat(StartMode.practice(PracticeScenario.DINER_ALLERGY).matches(dinerTable)).isFalse();
  }

  @Test
  @DisplayName("저장할 때 복습은 복습 카드를, 장소 연습은 상황을 대화에 표시하고 일반 대화는 아무것도 표시하지 않는다")
  void applyTo_marksHowConversationStarted() {
    Conversation story = conversation();
    StartMode.STORY.applyTo(story);
    Conversation review = conversation();
    StartMode.review(card(7L)).applyTo(review);
    Conversation practice = conversation();
    StartMode.practice(PracticeScenario.DINER_TABLE).applyTo(practice);

    assertThat(story.isReview()).isFalse();
    assertThat(story.isPractice()).isFalse();
    assertThat(review.getReviewCardId()).isEqualTo(7L);
    assertThat(review.isPractice()).isFalse();
    assertThat(practice.getPracticeScenario()).isEqualTo("DINER_TABLE");
    assertThat(practice.isReview()).isFalse();
  }

  @Test
  @DisplayName("일반 대화만 지금 퀘스트·기억·건넬 것을 섞고, 복습과 장소 연습은 정한 상황만 연습한다")
  void story_onlyForOrdinaryConversation() {
    assertThat(StartMode.STORY.story()).isTrue();
    assertThat(StartMode.review(card(1L)).story()).isFalse();
    assertThat(StartMode.practice(PracticeScenario.DINER_TABLE).story()).isFalse();
  }

  @Test
  @DisplayName("복습 카드와 연습 상황을 함께 주면 거절한다")
  void reviewAndPracticeTogether_isRejected() {
    assertThatThrownBy(() -> new StartMode(card(1L), PracticeScenario.DINER_TABLE))
        .isInstanceOf(IllegalArgumentException.class);
  }

  private static Conversation conversation() {
    return Conversation.start(1L, 2L, InitiatedBy.USER, 1, NOW);
  }

  private static ReviewCardResponse card(Long id) {
    return new ReviewCardResponse(
        id,
        10L,
        "CAFE",
        3L,
        new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원"),
        "음료를 주문했어요.",
        List.of(),
        0,
        null,
        List.of());
  }
}
