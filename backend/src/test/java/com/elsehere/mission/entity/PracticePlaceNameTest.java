package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class PracticePlaceNameTest {
  @Test
  void burgerShopUsesItsPlayerFacingName() {
    assertThat(PracticePlace.DINER.title()).isEqualTo("햄버거 가게");
  }

  @Test
  void storyAndQuestDescribeTheBurgerShop() {
    assertThat(TownStoryStage.DINER.title()).contains("햄버거 가게");
    assertThat(TownStoryStage.DINER.situation()).contains("햄버거 가게");
    assertThat(Quest.MORNING_AND_HAIRCUT.description()).contains("햄버거 가게");
  }
}
