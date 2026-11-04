package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;

/** 퀘스트 코인은 넉넉하게 100으로 시작해 퀘스트에서 사거나 지불하는 순간 그 값만큼 줄고, 퀘스트를 끝내면 보상만큼 늘어난다. 같은 구매로 두 번 줄지 않는다. */
class CoinsTest {
  @Test
  void everyoneStartsWithAHundredCoins() {
    assertThat(Coins.balance(null, null)).isEqualTo(100);
    assertThat(Coins.balance(FirstDayMission.start(8L), null)).isEqualTo(100);
  }

  @Test
  void orderingTheCafeDrinkPaysForIt() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());

    mission.accept(1, "CAFE_STAFF", List.of("order_drink"));

    assertThat(Coins.balance(mission, null)).isEqualTo(100 - 4);
  }

  @Test
  void finishingTheFirstQuestPaysOnceForTheDrinkAndTheUmbrellaThenAddsTheReward() {
    FirstDayMission mission = finishedFirstDay();

    mission.accept(5, "SHOP_STAFF", List.of("buy_umbrella", "choose_payment"));

    assertThat(Coins.balance(mission, null)).isEqualTo(100 - 4 - 8 + 10);
  }

  @Test
  void theLastPurchaseNamesWhatWasBought() {
    TownStory story = TownStory.start(8L);
    assertThat(Coins.lastPurchase(null, story)).isNull();

    story.accept(0, "DINER_STAFF", List.of("order_breakfast"));

    assertThat(Coins.lastPurchase(null, story)).isEqualTo("아침 식사");
    assertThat(Coins.lastPurchase(finishedFirstDay(), null)).isEqualTo("우산");
  }

  @Test
  void townStoryPurchasesAreTakenOutAsTheStoryGoes() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast"));
    assertThat(Coins.balance(null, story)).isEqualTo(100 - 7);

    while (!story.isCompleted()) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }

    // 산 것: 첫 퀘스트 12, 마을 이야기 아침 7·세탁 6·이발 15·꽃 5·간식 4·오락실 2·소포 5. 보상: 10 + 6 + 6 + 4 + 6
    assertThat(Coins.balance(finishedFirstDay(), story)).isEqualTo(100 - 12 - 44 + 32);
  }

  private static FirstDayMission finishedFirstDay() {
    FirstDayMission mission = FirstDayMission.start(8L);
    while (!mission.isCompleted()) {
      FirstDayStage stage = mission.current();
      mission.accept(stage.ordinal(), stage.npc(), stage.intentIds());
      if (stage == FirstDayStage.CAFE_BREAK) {
        mission.restAtCafe();
      }
    }
    return mission;
  }
}
