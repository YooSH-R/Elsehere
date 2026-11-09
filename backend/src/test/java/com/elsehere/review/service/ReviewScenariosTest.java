package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.mission.entity.FirstDayStage;
import java.time.Instant;
import org.junit.jupiter.api.Test;

class ReviewScenariosTest {
  @Test
  void sameCafeProducesDifferentScenariosForOrderingAndFindingTheBag() {
    var source = source();
    source.attachFirstDayStage(FirstDayStage.CAFE_BREAK.ordinal());
    var order = ReviewScenarios.capture(source, "CAFE_STAFF", "주문을 마쳤어요.");
    source.attachFirstDayStage(FirstDayStage.FIND_BAG.ordinal());
    var bag = ReviewScenarios.capture(source, "CAFE_STAFF", "가방을 받았어요.");
    assertThat(order.key()).isNotEqualTo(bag.key());
    assertThat(order.title()).isNotEqualTo(bag.title());
    assertThat(bag.situation()).contains("파란색");
    assertThat(String.join(" ", bag.learnerNotes())).contains("여권", "책");
    assertThat(bag.situation()).doesNotContain("가방을 받았어요");
  }

  @Test
  void wrongNpcNeverReceivesAnotherEmployeesScenario() {
    var source = source();
    source.attachFirstDayStage(FirstDayStage.CAFE_BREAK.ordinal());
    var hotel = ReviewScenarios.capture(source, "HOTEL_STAFF", "호텔 직원에게 카페 위치를 물었어요.");
    assertThat(hotel.key()).doesNotContain("CAFE_BREAK");
    assertThat(hotel.learnerNotes()).doesNotContain("마실 음료 이름 (예: latte, orange juice)");
  }

  @Test
  void missingOrInvalidOldStageUsesOnlySavedInformation() {
    var source = source();
    source.attachFirstDayStage(999);
    var scenario = ReviewScenarios.capture(source, "CAFE_STAFF", "검정 가방을 카페에 두고 왔다고 말했어요.");
    assertThat(scenario.situation()).contains("검정 가방");
    assertThat(scenario.situation()).doesNotContain("파란색");
    assertThat(scenario.learnerNotes()).isNotEmpty();
  }

  private static Conversation source() {
    return Conversation.start(1L, 2L, InitiatedBy.USER, 1, Instant.EPOCH);
  }
}
