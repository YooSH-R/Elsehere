package com.elsehere.conversation.dto;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class SpeechResponseTest {

  private static final Instant DELETED_AT = Instant.parse("2026-10-28T00:00:00Z");

  @Test
  @DisplayName("음성 키가 있으면 READY와 다시 듣기 주소를 돌려준다")
  void withKey_isReady() {
    assertThat(SpeechResponse.of(12L, "a.wav", null))
        .isEqualTo(new SpeechResponse(SpeechStatus.READY, "/api/v1/turns/12/speech"));
  }

  @Test
  @DisplayName("처음 만들 때 실패해 키가 없으면 FAILED로 다시 만들 수 있게 한다")
  void withoutKey_isFailed() {
    assertThat(SpeechResponse.of(12L, null, null))
        .isEqualTo(new SpeechResponse(SpeechStatus.FAILED, null));
  }

  @Test
  @DisplayName("보관 정책으로 지운 음성은 EXPIRED이며 다시 듣기 주소가 없다")
  void deletedByPolicy_isExpired() {
    assertThat(SpeechResponse.of(12L, null, DELETED_AT))
        .isEqualTo(new SpeechResponse(SpeechStatus.EXPIRED, null));
  }
}
