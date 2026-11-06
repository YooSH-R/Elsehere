package com.elsehere.conversation.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class ConversationTest {

  private static final Instant STARTED_AT = Instant.parse("2026-11-05T00:00:00Z");

  @Test
  @DisplayName("대화 시작 뒤 5분 미만은 시간 제한에 닿지 않고 정확히 5분부터 닿는다")
  void reachedTimeLimit_usesExactFiveMinuteBoundary() {
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, STARTED_AT);

    assertThat(conversation.reachedTimeLimit(STARTED_AT.plusSeconds(299))).isFalse();
    assertThat(conversation.reachedTimeLimit(STARTED_AT.plusSeconds(300))).isTrue();
    assertThat(conversation.reachedHardTimeLimit(STARTED_AT.plusSeconds(329))).isFalse();
    assertThat(conversation.reachedHardTimeLimit(STARTED_AT.plusSeconds(330))).isTrue();
  }

  @Test
  @DisplayName("마무리 구간에는 한 요청만 마지막 발화로 선점하고 실패하면 다시 열 수 있다")
  void finalTurnClaim_allowsOneRequestAndCanBeReleased() {
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, STARTED_AT);
    var first = java.util.UUID.randomUUID();
    var second = java.util.UUID.randomUUID();

    assertThat(conversation.claimFinalTurn(first)).isTrue();
    assertThat(conversation.claimFinalTurn(first)).isTrue();
    assertThat(conversation.claimFinalTurn(second)).isFalse();

    conversation.releaseFinalTurn(first);

    assertThat(conversation.claimFinalTurn(second)).isTrue();
  }

  @Test
  @DisplayName("시간 제한 종료는 TIME_LIMIT을 저장하고 다시 끝내도 최초 종료 시각을 보존한다")
  void endByTimeLimit_isIdempotent() {
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, STARTED_AT);
    Instant endedAt = STARTED_AT.plusSeconds(300);

    conversation.endByTimeLimit(endedAt);
    conversation.endByTimeLimit(endedAt.plusSeconds(10));

    assertThat(conversation.getStatus()).isEqualTo(ConversationStatus.ENDED_BY_NPC);
    assertThat(conversation.getEndReason()).isEqualTo(ConversationEndReason.TIME_LIMIT);
    assertThat(conversation.getEndedAt()).isEqualTo(endedAt);
  }
}
