package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.JudgeResult;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class PoliceDeskTest {

  @ParameterizedTest
  @ValueSource(
      strings = {"POLICE_OFFICER", "POLICE_SERGEANT", "POLICE_PATROL_OFFICER"})
  @DisplayName("경찰서 창구의 경찰관 세 명 모두 신고를 받는다")
  void serves_everyOfficerAtTheDesk(String npcCode) {
    assertThat(PoliceDesk.serves(npcCode)).isTrue();
  }

  @Test
  @DisplayName("다른 NPC에게 한 말은 신고로 받지 않는다")
  void serves_notOtherNpcs() {
    assertThat(PoliceDesk.serves("CAFE_STAFF")).isFalse();
    assertThat(PoliceDesk.serves("POLICE_ROOKIE")).isFalse(); // Tyler는 빵집 야간 직원으로 전직했다.
  }

  @Test
  @DisplayName("단어가 아니라 문맥으로 신고를 고르게, 판정 상태에 출동할 일의 설명과 마을 전체 장소를 담는다")
  void state_offersReportNoPlaceAndChatWithEveryTownPlace() {
    assertThat(PoliceDesk.STATE.intents())
        .extracting(intent -> intent.id())
        .containsExactly("report_incident", "no_place", "chat");
    assertThat(PoliceDesk.STATE.slots().option().slot()).isEqualTo("place");
    assertThat(PoliceDesk.STATE.slots().option().labels())
        .contains(
            "Corner Cafe",
            "Maple Bakery",
            "the park with the fountain",
            "Little Arcade (claw machines) or Paper & Leaf Books above it",
            "Home & Living (furniture store) or Leaf Tea Room above it",
            "the ice cream truck plaza",
            "the main street")
        .hasSize(16);
  }

  @Test
  @DisplayName("신고와 장소를 들으면 그 장소 코드로 출동한다")
  void dispatch_toTheReportedPlace() {
    var dispatch = PoliceDesk.dispatch(judged("report_incident", "the park with the fountain"));

    assertThat(dispatch)
        .hasValueSatisfying(value -> assertThat(value.placeCode()).isEqualTo("PARK"));
  }

  @Test
  @DisplayName("본길에서 생긴 일이면 장소 없이 본길을 순찰한다")
  void dispatch_patrolsTheMainStreet() {
    var dispatch = PoliceDesk.dispatch(judged("report_incident", "the main street"));

    assertThat(dispatch).hasValueSatisfying(value -> assertThat(value.placeCode()).isNull());
  }

  @Test
  @DisplayName("장소를 모른다고 하면 본길을 순찰한다")
  void dispatch_patrolsWhenTheyDoNotKnowWhere() {
    var dispatch = PoliceDesk.dispatch(judged("no_place", "none"));

    assertThat(dispatch).hasValueSatisfying(value -> assertThat(value.placeCode()).isNull());
  }

  @Test
  @DisplayName("신고했지만 장소를 아직 말하지 않았으면 출동하지 않고 어디서 생긴 일인지 묻는다")
  void dispatch_waitsForThePlace() {
    JudgeResult judge = judged("report_incident", "none");

    assertThat(PoliceDesk.dispatch(judge)).isEmpty();
    assertThat(PoliceDesk.instruction(judge, PoliceDesk.dispatch(judge))).contains("Ask where");
  }

  @Test
  @DisplayName("신고가 아닌 말에는 출동하지 않고, 경찰관이 순찰차가 나간다고 말하지 않게 한다")
  void dispatch_notForOtherTalk() {
    JudgeResult judge = judged("chat", "Corner Cafe");

    assertThat(PoliceDesk.dispatch(judge)).isEmpty();
    assertThat(PoliceDesk.instruction(judge, PoliceDesk.dispatch(judge)))
        .contains("Do not say a patrol car is going out");
  }

  @Test
  @DisplayName("판정이 실패하면 출동하지 않는다")
  void dispatch_notWhenTheJudgeFailed() {
    JudgeResult judge =
        new JudgeResult(
            "Someone took my bag at the cafe.",
            false,
            null,
            null,
            List.of(),
            new JudgeResult.Intent("unclear", List.of("unclear"), Map.of(), "timeout"));

    assertThat(PoliceDesk.dispatch(judge)).isEmpty();
  }

  @Test
  @DisplayName("출동하는 턴에는 경찰관이 직접 순찰차로 그 장소에 간다고 말하고 대화를 마친다")
  void instruction_saysTheOfficerDrivesOutNow() {
    JudgeResult judge = judged("report_incident", "Maple Bakery");

    String instruction = PoliceDesk.instruction(judge, PoliceDesk.dispatch(judge));

    assertThat(instruction).contains("Maple Bakery").contains("yourself in the patrol car");
  }

  private static JudgeResult judged(String intent, String place) {
    return new JudgeResult(
        "text",
        false,
        null,
        null,
        List.of(),
        new JudgeResult.Intent(intent, List.of(intent), Map.of("place", place), null));
  }
}
