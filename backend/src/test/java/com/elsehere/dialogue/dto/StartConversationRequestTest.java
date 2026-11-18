package com.elsehere.dialogue.dto;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.InitiatedBy;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class StartConversationRequestTest {

  @Test
  @DisplayName("NPC 욕구는 공통 규칙의 욕구 키만 남기고 0~100으로 자른다")
  void needsKeepOnlyKnownKeysWithinRange() {
    StartConversationRequest request =
        withNeeds(Map.of("HUNGER", 120.0, "SOCIAL", -5.0, "ENERGY", 35.5, "THIRST", 10.0));

    assertThat(request.needs())
        .containsExactlyInAnyOrderEntriesOf(Map.of("HUNGER", 100.0, "SOCIAL", 0.0, "ENERGY", 35.5));
  }

  @Test
  @DisplayName("욕구를 보내지 않는 클라이언트(생활하지 않는 NPC, 이전 버전)는 null이다")
  void missingNeedsStayNull() {
    assertThat(withNeeds(null).needs()).isNull();
  }

  private static StartConversationRequest withNeeds(Map<String, Double> needs) {
    return new StartConversationRequest(
        UUID.randomUUID(), "HOTEL_STAFF", InitiatedBy.USER, false, null, null, null, needs);
  }
}
