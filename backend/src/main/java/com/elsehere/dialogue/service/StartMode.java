package com.elsehere.dialogue.service;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.review.dto.ReviewCardResponse;
import java.util.Objects;

/**
 * 대화를 어떻게 시작했는지. 일반 대화, 상황 복습, 장소 연습(PRC-02) 중 하나다.
 *
 * @param review 상황 복습이면 그 복습 카드. 아니면 null
 * @param practice 장소 연습이면 고른 상황. 아니면 null
 */
record StartMode(ReviewCardResponse review, PracticeScenario practice) {

  static final StartMode STORY = new StartMode(null, null);

  StartMode {
    if (review != null && practice != null) {
      throw new IllegalArgumentException("복습과 장소 연습을 함께 시작할 수 없다");
    }
  }

  static StartMode review(ReviewCardResponse card) {
    return new StartMode(Objects.requireNonNull(card), null);
  }

  static StartMode practice(PracticeScenario scenario) {
    return new StartMode(null, Objects.requireNonNull(scenario));
  }

  /** 지금 퀘스트·기억·건넬 것을 섞는 일반 대화인지. 복습과 장소 연습은 정한 상황만 연습한다. */
  boolean story() {
    return review == null && practice == null;
  }

  boolean isReview() {
    return review != null;
  }

  Long reviewCardId() {
    return review == null ? null : review.id();
  }

  String practiceScenarioId() {
    return practice == null ? null : practice.name();
  }

  /** 같은 요청 ID로 저장된 대화가 같은 방식(같은 복습 카드·같은 연습 상황)으로 시작했는지. */
  boolean matches(Conversation conversation) {
    return Objects.equals(conversation.getReviewCardId(), reviewCardId())
        && Objects.equals(conversation.getPracticeScenario(), practiceScenarioId());
  }

  /** 새로 만든 대화에 어떻게 시작했는지 표시한다. */
  void applyTo(Conversation conversation) {
    if (review != null) {
      conversation.attachReviewCard(review.id());
    }
    if (practice != null) {
      conversation.attachPracticeScenario(practice.name());
    }
  }
}
