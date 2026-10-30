package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class FirstDayStageTest {

  @Test
  @DisplayName("비 오는 거리의 행인은 우산을 파는 DRUGSTORE 간판으로 길을 알려 준다")
  void rainyStreet_sendsLearnerToTheDrugstoreSign() {
    FirstDayStage stage = FirstDayStage.RAINY_STREET;

    assertThat(stage.instruction()).contains("DRUGSTORE").doesNotContainIgnoringCase("convenience");
    assertThat(stage.goals())
        .filteredOn(goal -> goal.id().equals("ask_directions"))
        .singleElement()
        .satisfies(
            goal -> {
              assertThat(goal.title()).isEqualTo("드럭스토어 가는 길 묻기");
              assertThat(goal.description()).contains("drugstore");
            });
  }

  @Test
  @DisplayName("우산을 사는 단계는 드럭스토어에서 일어난다")
  void buyUmbrella_happensAtTheDrugstore() {
    assertThat(FirstDayStage.BUY_UMBRELLA.situation()).startsWith("우산을 파는 드럭스토어예요.");
  }
}
