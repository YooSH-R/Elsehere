package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.mission.entity.ShowcaseStage;
import java.time.Instant;
import org.junit.jupiter.api.Test;

class ShowcaseReviewSnapshotTest {
  @Test
  void burgerReviewRestartsTheOrderAndKeepsItsDetailsAfterTheTrip() {
    Conversation source = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    source.attachShowcaseStage(ShowcaseStage.ORDER.ordinal());
    source.recordShowcaseGoals(ShowcaseStage.ORDER.goalIds());
    var snapshot = ReviewScenarios.capture(source, "DINER_STAFF", "주문을 마치고 호텔로 돌아왔어요.");
    assertThat(snapshot.key()).startsWith("showcase_ORDER");
    assertThat(snapshot.title()).isEqualTo("햄버거 주문 다시 연습");
    assertThat(snapshot.situation()).contains("치즈버거", "양파", "감자튀김", "콜라");
    assertThat(String.join(" ", snapshot.learnerNotes()))
        .contains("포장", "매장")
        .doesNotContain("세탁", "흘렸");
  }
}
