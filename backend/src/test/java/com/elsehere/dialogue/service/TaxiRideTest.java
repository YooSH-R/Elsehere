package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.JudgeResult;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class TaxiRideTest {

  @Test
  @DisplayName("택시 기사에게 한 말만 목적지로 판정한다")
  void serves_onlyTheTaxiDriver() {
    assertThat(TaxiRide.serves("TAXI_DRIVER")).isTrue();
    assertThat(TaxiRide.serves("CAFE_STAFF")).isFalse();
  }

  @Test
  @DisplayName("단어가 아니라 문맥으로 목적지를 고르게, 판정 상태에 최근 대화와 택시가 가는 마을 장소 전체를 담는다")
  void state_offersGoToAndChatWithEveryTaxiDestination() {
    assertThat(TaxiRide.STATE.intents())
        .extracting(intent -> intent.id())
        .containsExactly("go_to", "chat");
    assertThat(TaxiRide.STATE.slots().option().slot()).isEqualTo("destination");
    assertThat(TaxiRide.STATE.slots().option().labels())
        .contains(
            "Corner Cafe",
            "the learner's home",
            "Bloom Flowers (flower shop)",
            "the police station")
        .doesNotContain("Maple Bakery")
        .hasSize(11);

    var json = tools.jackson.databind.json.JsonMapper.builder().build().valueToTree(TaxiRide.STATE);
    assertThat(json.path("recent_lines").asInt()).isEqualTo(6);
  }

  @Test
  @DisplayName("목적지를 말하면 그 장소 코드로 출발한다")
  void destination_toThePlaceTheyAskedFor() {
    var destination = TaxiRide.destination(judged(List.of("go_to"), "the police station"));

    assertThat(destination)
        .hasValueSatisfying(value -> assertThat(value.placeCode()).isEqualTo("POLICE_STATION"));
  }

  @Test
  @DisplayName("한 줄에서 다른 말 뒤에 목적지를 말해도 출발한다")
  void destination_afterAnotherIntentInTheSameLine() {
    var destination = TaxiRide.destination(judged(List.of("chat", "go_to"), "Maple Diner"));

    assertThat(destination)
        .hasValueSatisfying(value -> assertThat(value.placeCode()).isEqualTo("DINER"));
  }

  @Test
  @DisplayName("가자고 했지만 택시가 가는 곳이 아니거나 어느 곳인지 모르면 출발하지 않고, 갈 수 있는 곳을 알려 주며 묻는다")
  void destination_waitsWhenThePlaceIsUnknown() {
    JudgeResult judge = judged(List.of("go_to"), "none");

    assertThat(TaxiRide.destination(judge)).isEmpty();
    assertThat(TaxiRide.instruction(judge, TaxiRide.destination(judge)))
        .contains("Ask where they want to go")
        .contains("the post office")
        .contains("the police station")
        .contains("their home")
        .doesNotContain("the learner's home");
  }

  @Test
  @DisplayName("택시가 가지 않는 곳으로 가자고 하면 기사가 승낙하지 않고, 못 간다고 말한 뒤 가장 가까운 하차 장소를 권한다")
  void instruction_refusesAPlaceTheTaxiDoesNotGoTo() {
    JudgeResult judge = judged(List.of("go_to"), "none");

    String instruction = TaxiRide.instruction(judge, TaxiRide.destination(judge));

    assertThat(instruction)
        .contains("say plainly that your taxi does not go there")
        .contains("the park with the fountain")
        .contains("Maple Bakery")
        .contains("Little Arcade")
        .contains("the ice cream truck")
        .contains("Do not agree to go there");
  }

  @Test
  @DisplayName("갈 곳을 권할 때는 택시가 가는 곳 안에서만 고르게 한다")
  void instruction_recommendsOnlyPlacesTheTaxiGoesTo() {
    JudgeResult judge = judged(List.of("chat"), "none");

    String instruction = TaxiRide.instruction(judge, TaxiRide.destination(judge));

    assertThat(instruction)
        .contains("suggest or recommend places, choose only from")
        .contains("Suds Laundromat")
        .contains("their home")
        .contains("Your taxi does not go to the park with the fountain");
  }

  @Test
  @DisplayName("목적지가 아닌 말에는 출발하지 않고, 기사가 부탁받지 않은 곳으로 간다고 말하지 않게 한다")
  void destination_notForOtherTalk() {
    JudgeResult judge = judged(List.of("chat"), "Corner Cafe");

    assertThat(TaxiRide.destination(judge)).isEmpty();
    assertThat(TaxiRide.instruction(judge, TaxiRide.destination(judge)))
        .contains("Do not say you are driving them to a place they did not ask for");
  }

  @Test
  @DisplayName("판정이 실패하면 출발하지 않는다")
  void destination_notWhenTheJudgeFailed() {
    JudgeResult judge =
        new JudgeResult(
            "Take me to the cafe.",
            false,
            null,
            null,
            List.of(),
            new JudgeResult.Intent("unclear", List.of("unclear"), Map.of(), "timeout"));

    assertThat(TaxiRide.destination(judge)).isEmpty();
  }

  @Test
  @DisplayName("출발하는 턴에는 기사가 지금 그 장소로 간다고 말한다")
  void instruction_saysTheDriverHeadsThereNow() {
    JudgeResult judge = judged(List.of("go_to"), "Town Bank");

    String instruction = TaxiRide.instruction(judge, TaxiRide.destination(judge));

    assertThat(instruction).contains("Town Bank").contains("driving them there now");
  }

  private static JudgeResult judged(List<String> intents, String place) {
    return new JudgeResult(
        "text",
        false,
        null,
        null,
        List.of(),
        new JudgeResult.Intent(intents.get(0), intents, Map.of("destination", place), null));
  }
}
