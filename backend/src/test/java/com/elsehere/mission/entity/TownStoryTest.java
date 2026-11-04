package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;

class TownStoryTest {
  @Test
  void dinerGoalsAdvanceOnlyForTheCurrentNpc() {
    TownStory story = TownStory.start(8L);

    story.accept(0, "BANK_STAFF", List.of("order_breakfast", "explain_spill"));
    assertThat(story.getStage()).isZero();

    story.accept(0, "DINER_STAFF", List.of("order_breakfast", "explain_spill"));
    assertThat(story.getStage()).isEqualTo(1);
  }

  @Test
  void laundryGoalsAccumulateAndOldResultsCannotAdvanceTheStory() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast", "explain_spill"));

    story.accept(0, "DINER_STAFF", List.of("order_breakfast", "explain_spill"));
    story.accept(1, "LAUNDROMAT_STAFF", List.of("ask_laundry"));
    assertThat(story.getStage()).isEqualTo(1);

    story.accept(1, "LAUNDROMAT_STAFF", List.of("ask_cost"));
    assertThat(story.getStage()).isEqualTo(2);
  }

  // 퀘스트 하나를 끝내면 이야기가 멈추고, 다음 퀘스트를 시작해야 이어진다.
  @Test
  void storyStopsAtTheEndOfAQuestUntilTheNextQuestStarts() {
    TownStory story = TownStory.start(8L);
    while (story.getStage() < 7) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    assertThat(story.isPaused()).isTrue();

    story.accept(7, "BAKERY_STAFF", List.of("choose_snack"));
    assertThat(story.getStage()).isEqualTo(7);

    story.resume();
    story.accept(7, "BAKERY_STAFF", List.of("choose_snack"));
    assertThat(story.getStage()).isEqualTo(8);
    assertThat(story.isPaused()).isFalse();
  }

  @Test
  void travelerFollowsTheWholeConnectedStory() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast", "explain_spill"));
    story.accept(1, "LAUNDROMAT_STAFF", List.of("ask_laundry", "ask_cost"));
    story.accept(2, "BANK_STAFF", List.of("ask_atm", "ask_withdrawal"));
    story.accept(3, "LAUNDROMAT_STAFF", List.of("request_wash"));
    story.accept(4, "BARBER_STAFF", List.of("ask_haircut", "describe_style"));
    story.accept(5, "FLOWER_SHOP_STAFF", List.of("choose_flower", "ask_wrapping"));
    story.accept(6, "BARBER_STAFF", List.of("offer_flower", "thank_barber"));
    // 두 번째 퀘스트를 끝내면 멈추므로 세 번째 퀘스트를 시작한다.
    story.resume();
    story.accept(7, "BAKERY_STAFF", List.of("choose_snack"));
    story.accept(8, "LAUNDROMAT_STAFF", List.of("collect_laundry", "thank_laundromat"));
    story.accept(9, "HOME_STORE_STAFF", List.of("ask_lamp", "ask_shipping"));
    story.accept(10, "ARCADE_STAFF", List.of("ask_game"));
    story.accept(11, "POST_OFFICE_STAFF", List.of("ask_parcel", "ask_price"));
    assertThat(story.getStage()).isEqualTo(12);
  }
}
