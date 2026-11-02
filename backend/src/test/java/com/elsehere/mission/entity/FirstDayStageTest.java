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
  @DisplayName("첫 호텔 대화는 체크인을 끝내지 않고 얼리 체크인이 어렵다고 알린 뒤 짐 보관과 카페를 권한다")
  void earlyArrival_keepsCheckInForLater() {
    String instruction = FirstDayStage.EARLY_ARRIVAL.instruction();

    assertThat(instruction)
        .contains("Do not check the learner in")
        .contains("room key")
        .contains("ID, a credit card or payment")
        .contains("early check-in is not possible")
        .contains("store the large suitcase")
        .contains("Corner Cafe");
  }

  @Test
  @DisplayName("호텔 직원은 예약 확인·짐 보관·카페 안내를 학습자가 먼저 요청하기 전에는 꺼내지 않고, 퀘스트 카드는 직원에게 요청하라고 안내한다")
  void earlyArrival_waitsForTheLearnerToAsk() {
    FirstDayStage stage = FirstDayStage.EARLY_ARRIVAL;

    assertThat(stage.instruction()).contains("Do not offer any of these before the learner asks");
    assertThat(stage.direction()).isEqualTo("호텔 직원에게 요청하세요.");
  }

  @Test
  @DisplayName("가방 분실·가방 찾기·비 단계의 NPC도 학습자가 말하기 전에 사건이나 해결책을 먼저 꺼내지 않는다")
  void incidentStages_waitForTheLearnerToBringThemUp() {
    assertThat(FirstDayStage.MISSING_BAG.instruction())
        .contains("Do not bring up the missing bag before the learner explains it");
    assertThat(FirstDayStage.FIND_BAG.instruction())
        .contains("Do not mention or offer the found bag before the learner asks about a lost bag");
    assertThat(FirstDayStage.RAINY_STREET.instruction())
        .contains("Do not suggest umbrellas or the drugstore before the learner asks");
  }

  @Test
  @DisplayName("드럭스토어 점원도 우산과 가격을 먼저 권하지 않고, 결제 방법은 학습자가 사겠다고 한 뒤에 묻는다")
  void buyUmbrella_waitsForTheLearnerToAsk() {
    assertThat(FirstDayStage.BUY_UMBRELLA.instruction())
        .contains("Do not offer an umbrella or say its price before the learner asks")
        .contains("only after the learner decides to buy");
  }

  @Test
  @DisplayName("우산을 사는 단계는 드럭스토어에서 일어난다")
  void buyUmbrella_happensAtTheDrugstore() {
    assertThat(FirstDayStage.BUY_UMBRELLA.situation()).startsWith("우산을 파는 드럭스토어예요.");
  }
}
