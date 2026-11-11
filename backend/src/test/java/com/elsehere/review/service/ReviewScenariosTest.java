package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.mission.entity.FirstDayStage;
import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class ReviewScenariosTest {
  @ParameterizedTest
  @ValueSource(booleans = {false, true})
  void cafeReviewOnlyRepeatsGoalsFromThatConversation(boolean orderFinished) {
    var source = source();
    source.attachFirstDayStage(FirstDayStage.CAFE_BREAK.ordinal());
    source.recordFirstDayGoals(orderFinished ? List.of() : List.of("dine_in"));

    var scenario = ReviewScenarios.capture(source, "CAFE_STAFF", "직원에게 감사 인사를 했어요.");

    assertThat(String.join(" ", scenario.learnerNotes())).doesNotContain("마실 음료 이름", "크기나 온도");
    if (orderFinished) {
      assertThat(scenario.key()).isEqualTo("saved_conversation");
      assertThat(scenario.situation()).isEqualTo("직원에게 감사 인사를 했어요.");
    } else {
      assertThat(scenario.learnerNotes()).containsExactly("여기서 마시겠다고 말하기");
    }
  }

  @Test
  void dinerReviewKeepsOrderingAndSpillAsSeparateConversations() {
    var orderSource = source();
    orderSource.attachTownStoryStage(0);
    orderSource.recordTownStoryGoals(List.of("order_breakfast"));
    var order = ReviewScenarios.capture(orderSource, "DINER_STAFF", "주문을 마쳤어요.");
    var spillSource = source();
    spillSource.attachTownStoryStage(0);
    spillSource.recordTownStoryGoals(List.of("explain_spill"));
    var spill = ReviewScenarios.capture(spillSource, "DINER_STAFF", "옷에 음료를 쏟았다고 말했어요.");

    assertThat(order.key()).isNotEqualTo(spill.key());
    assertThat(order.learnerNotes()).hasSize(1);
    assertThat(order.situation()).contains("음식과 음료").doesNotContain("쏟", "세탁");
    assertThat(spill.learnerNotes()).hasSize(1);
    assertThat(spill.situation()).contains("무엇을 어디에").doesNotContain("세탁", "아침 주문");
  }

  @Test
  void sameCafeProducesDifferentScenariosForOrderingAndFindingTheBag() {
    var source = source();
    source.attachFirstDayStage(FirstDayStage.CAFE_BREAK.ordinal());
    source.recordFirstDayGoals(FirstDayStage.CAFE_BREAK.intentIds());
    var order = ReviewScenarios.capture(source, "CAFE_STAFF", "주문을 마쳤어요.");
    source = source();
    source.attachFirstDayStage(FirstDayStage.FIND_BAG.ordinal());
    source.recordFirstDayGoals(FirstDayStage.FIND_BAG.intentIds());
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
    source.recordFirstDayGoals(FirstDayStage.CAFE_BREAK.intentIds());
    var hotel = ReviewScenarios.capture(source, "HOTEL_STAFF", "호텔 직원에게 카페 위치를 물었어요.");
    assertThat(hotel.key()).doesNotContain("CAFE_BREAK");
    assertThat(hotel.learnerNotes()).doesNotContain("마실 음료 이름");
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
