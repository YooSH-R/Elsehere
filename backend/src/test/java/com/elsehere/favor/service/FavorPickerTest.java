package com.elsehere.favor.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.favor.service.FavorPicker.Pick;
import com.elsehere.favor.service.FavorPicker.Situation;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

// 돌발 부탁 고르기(설계 7.4·7.6). 실제로 내보낸 규칙(content/npc-favors.json)으로 확인한다.
class FavorPickerTest {

  private static final FavorRules RULES = new FavorRules();
  private static final Map<String, Double> FINE =
      Map.of("HUNGER", 80.0, "ENERGY", 80.0, "SOCIAL", 80.0, "FUN", 80.0, "WORK", 80.0);

  @Test
  @DisplayName("근무 중 배고픈 호텔 직원은 다른 가게의 먹을 것을 사다 달라고 부탁한다")
  void hungryStaffAsksForFoodFromAnotherShop() {
    Pick pick = pick(staff("HOTEL_STAFF", "HOTEL", needs("HUNGER", 20.0), 10 * 60)).orElseThrow();

    assertThat(pick.request().id()).isEqualTo("bring_food");
    assertThat(pick.offerPlace()).isIn("CAFE", "BAKERY", "DINER");
    assertThat(pick.offer().fills()).containsKey("HUNGER");
  }

  @Test
  @DisplayName("카페 직원은 자기 카페의 머핀을 부탁하지 않는다")
  void cafeStaffDoesNotSendTheLearnerToItsOwnCafe() {
    for (int day = 1; day <= 20; day++) {
      Situation hungry = staff("CAFE_STAFF", "CAFE", needs("HUNGER", 10.0), 10 * 60, day);

      assertThat(pick(hungry).orElseThrow().offerPlace()).isNotEqualTo("CAFE");
    }
  }

  @Test
  @DisplayName("같은 날 같은 NPC는 늘 같은 부탁을 하고, 날이 바뀌면 해결처가 달라질 수 있다")
  void theSameDayGivesTheSamePick() {
    Situation hungry = staff("HOTEL_STAFF", "HOTEL", needs("HUNGER", 20.0), 10 * 60);

    assertThat(pick(hungry)).isEqualTo(pick(hungry));
    assertThat(
            java.util.stream.IntStream.rangeClosed(1, 20)
                .mapToObj(
                    day ->
                        pick(staff("HOTEL_STAFF", "HOTEL", needs("HUNGER", 20.0), 10 * 60, day))
                            .orElseThrow()
                            .offerPlace())
                .distinct()
                .count())
        .isGreaterThan(1);
  }

  @Test
  @DisplayName("욕구가 넉넉하고 시각이 맞는 사건도 없으면 부탁하지 않는다")
  void nothingWhenNeedsAreFineAndNoSituationFits() {
    assertThat(pick(staff("HOTEL_STAFF", "HOTEL", FINE, 10 * 60))).isEmpty();
  }

  @Test
  @DisplayName("점심 때 카페 직원은 바쁜 시간 사건을 그 대화 안에서 꺼낸다")
  void lunchRushAtTheCafe() {
    Pick pick = pick(staff("CAFE_STAFF", "CAFE", FINE, 12 * 60 + 30)).orElseThrow();

    assertThat(pick.request().id()).isEqualTo("rush_hour");
    assertThat(pick.offer()).isNull();
  }

  @Test
  @DisplayName("학습자가 그날 이미 겪은 상황 사건은 다시 꺼내지 않는다")
  void aSituationHappensOncePerLearnerDay() {
    Situation lunch = staff("CAFE_STAFF", "CAFE", FINE, 12 * 60 + 30);
    Situation again =
        new Situation(
            lunch.npcCode(),
            lunch.role(),
            lunch.workplace(),
            lunch.activity(),
            lunch.needs(),
            lunch.day(),
            lunch.minuteOfDay(),
            false,
            lunch.shiftLeftMinutes(),
            Set.of("rush_hour"));

    assertThat(pick(again)).isEmpty();
  }

  @Test
  @DisplayName("품절 사건은 그날 그 학습자와의 첫 대화에서만 나온다")
  void soldOutOnlyOnTheFirstTalkOfTheDay() {
    for (int day = 1; day <= 20; day++) {
      Situation later = staff("CAFE_STAFF", "CAFE", FINE, 15 * 60, day);

      assertThat(pick(later).map(value -> value.request().id()))
          .isNotEqualTo(Optional.of("sold_out"));
    }
  }

  @Test
  @DisplayName("교대가 곧 끝나는 직원은 심부름 대신 그 대화 안에서 끝나는 부탁만 한다")
  void staffAboutToLeaveAsksOnlyWhatEndsInTheTalk() {
    Situation leaving =
        new Situation(
            "HOTEL_RECEPTIONIST",
            "STAFF",
            "HOTEL",
            "WORK",
            needs("HUNGER", 10.0, "SOCIAL", 10.0),
            3,
            10 * 60,
            false,
            100,
            Set.of());

    assertThat(pick(leaving).orElseThrow().request().id()).isEqualTo("recommend_a_spot");
  }

  @Test
  @DisplayName("여가 중인 행인은 외로우면 구경할 곳을 묻고, 배고파도 심부름은 시키지 않는다")
  void aPasserbyOnlyAsksForARecommendation() {
    Situation lonely =
        new Situation(
            "TOWN_PASSERBY",
            "PASSERBY",
            "TOWN",
            "FREE",
            needs("HUNGER", 10.0, "SOCIAL", 20.0),
            3,
            15 * 60,
            false,
            1440,
            Set.of());

    assertThat(pick(lonely).orElseThrow().request().id()).isEqualTo("recommend_a_spot");
  }

  @Test
  @DisplayName("편지 심부름은 NPC가 먼저 말을 걸 때만 꺼내고, 학습자가 건 대화에서는 꺼내지 않는다")
  void aLetterErrandOnlyComesWithAnNpcCall() {
    Situation afternoon = staff("HOTEL_STAFF", "HOTEL", FINE, 14 * 60);

    assertThat(pick(afternoon)).isEmpty();
    assertThat(FavorPicker.pick(RULES, afternoon, FavorPicker.CALL).orElseThrow().request().id())
        .isEqualTo("letter_to_send");
  }

  @Test
  @DisplayName("주문 상황 사건은 학습자가 건 대화에서만 나오고, NPC가 말을 걸며 꺼내지 않는다")
  void anOrderingSituationNeverComesWithAnNpcCall() {
    Situation lunch = staff("CAFE_STAFF", "CAFE", FINE, 12 * 60 + 30);

    assertThat(FavorPicker.pick(RULES, lunch, FavorPicker.CALL)).isEmpty();
    assertThat(
            FavorPicker.pick(RULES, lunch, FavorPicker.TALK_SITUATION).orElseThrow().request().id())
        .isEqualTo("rush_hour");
    assertThat(FavorPicker.pick(RULES, lunch, FavorPicker.TALK_NEED)).isEmpty();
  }

  private static Optional<Pick> pick(Situation situation) {
    return FavorPicker.pick(RULES, situation, FavorPicker.TALK);
  }

  private static Situation staff(
      String npcCode, String workplace, Map<String, Double> needs, int minuteOfDay) {
    return staff(npcCode, workplace, needs, minuteOfDay, 3);
  }

  private static Situation staff(
      String npcCode, String workplace, Map<String, Double> needs, int minuteOfDay, int day) {
    return new Situation(
        npcCode, "STAFF", workplace, "WORK", needs, day, minuteOfDay, false, 1440, Set.of());
  }

  private static Map<String, Double> needs(Object... pairs) {
    var result = new java.util.HashMap<>(FINE);
    for (int i = 0; i < pairs.length; i += 2) {
      result.put((String) pairs[i], (Double) pairs[i + 1]);
    }
    return result;
  }
}
