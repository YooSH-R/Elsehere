package com.elsehere.conversation.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class ConversationTest {

  private static final Instant STARTED_AT = Instant.parse("2026-11-05T00:00:00Z");

  @Test
  void travelSnapshotKeepsTheFirstConversationSituation() {
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, STARTED_AT);
    conversation.recordNpcTravelSituation("walked 80 metres and arrived at work");
    conversation.recordNpcTravelSituation("going home");
    assertThat(conversation.getNpcTravelSituation())
        .isEqualTo("walked 80 metres and arrived at work");
  }

  @Test
  @DisplayName("이미 사용자가 끝낸 대화는 턴 제한으로 다시 끝내지 않는다")
  void endByTurnLimit_keepsEarlierEnd() {
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, STARTED_AT);
    conversation.endByUser(STARTED_AT.plusSeconds(10));
    conversation.endByTurnLimit(STARTED_AT.plusSeconds(20));
    assertThat(conversation.getEndReason()).isEqualTo(ConversationEndReason.USER_ENDED);
    assertThat(conversation.getEndedAt()).isEqualTo(STARTED_AT.plusSeconds(10));
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
  @DisplayName("턴 제한 종료는 TURN_LIMIT을 저장하고 다시 끝내도 최초 종료 시각을 보존한다")
  void endByTurnLimit_isIdempotent() {
    Conversation conversation = Conversation.start(1L, 2L, InitiatedBy.USER, 1, STARTED_AT);
    Instant endedAt = STARTED_AT.plusSeconds(300);

    conversation.endByTurnLimit(endedAt);
    conversation.endByTurnLimit(endedAt.plusSeconds(10));

    assertThat(conversation.getStatus()).isEqualTo(ConversationStatus.ENDED_BY_NPC);
    assertThat(conversation.getEndReason()).isEqualTo(ConversationEndReason.TURN_LIMIT);
    assertThat(conversation.getEndedAt()).isEqualTo(endedAt);
  }
}
