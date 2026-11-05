package com.elsehere.followup.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class NpcFollowUpTest {

  private static final Instant PROMISED_AT = Instant.parse("2026-11-04T05:00:00Z");

  @ParameterizedTest
  @CsvSource({"QUICK, 45", "SHORT, 90", "LONG, 180"})
  @DisplayName("기다림 정도마다 정해진 초 뒤에 준비된다")
  void promise_readyAfterTheWaitOfItsKind(FollowUpWait wait, long seconds) {
    NpcFollowUp followUp =
        NpcFollowUp.promise(1L, 2L, 3L, "iced latte", "아이스 라테", wait, PROMISED_AT);

    assertThat(followUp.getReadyAt()).isEqualTo(PROMISED_AT.plusSeconds(seconds));
  }

  @ParameterizedTest
  @CsvSource({"0, 45", "200, 45", "44100, 1", "45000, 0", "60000, 0"})
  @DisplayName("남은 초는 올림으로 세고, 준비 시각부터는 0이다")
  void remainingSeconds_roundsUpAndStopsAtZero(long elapsedMillis, long remaining) {
    NpcFollowUp followUp = quickLatte();

    assertThat(followUp.remainingSeconds(PROMISED_AT.plusMillis(elapsedMillis)))
        .isEqualTo(remaining);
  }

  @Test
  @DisplayName("준비 시각 전에는 준비되지 않았고, 준비 시각부터 준비됐다")
  void isReady_fromTheReadyTime() {
    NpcFollowUp followUp = quickLatte();

    assertThat(followUp.isReady(PROMISED_AT.plusSeconds(44))).isFalse();
    assertThat(followUp.isReady(PROMISED_AT.plusSeconds(45))).isTrue();
  }

  @Test
  @DisplayName("건네면 건넨 시각과 건넨 대화를 남긴다")
  void deliver_recordsTheConversationThatHandedItOver() {
    NpcFollowUp followUp = quickLatte();
    Instant now = PROMISED_AT.plusSeconds(60);

    followUp.deliver(9L, now);

    assertThat(followUp.isDelivered()).isTrue();
    assertThat(followUp.getDeliveredAt()).isEqualTo(now);
    assertThat(followUp.getDeliveredConversationId()).isEqualTo(9L);
  }

  private static NpcFollowUp quickLatte() {
    return NpcFollowUp.promise(1L, 2L, 3L, "iced latte", "아이스 라테", FollowUpWait.QUICK, PROMISED_AT);
  }
}
