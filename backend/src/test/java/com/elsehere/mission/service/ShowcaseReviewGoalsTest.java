package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;

// 시연 상황을 [다시 연습]할 때도 그 단계의 조건을 처음부터 판정한다. 진행 중인 시연과는 상관없다.
class ShowcaseReviewGoalsTest {
  private static final List<String> ORDER =
      List.of("order_burger", "customize_burger", "add_sides_drink", "choose_dining");

  @Test
  void orderReviewJudgesEveryOrderConditionFromTheStart() {
    QuestTalk.Goals goals =
        ShowcaseReviewGoals.talk("showcase_ORDER", List.of()).orElseThrow().goals().orElseThrow();

    assertThat(goals.remainingGoals()).containsExactlyElementsOf(ORDER);
    assertThat(goals.state().intents())
        .extracting(intent -> intent.id())
        .containsExactlyElementsOf(ORDER);
  }

  @Test
  void conditionsAlreadySaidAreNotJudgedAgain() {
    QuestTalk talk =
        ShowcaseReviewGoals.talk("showcase_ORDER", List.of("order_burger")).orElseThrow();
    assertThat(talk.goals().orElseThrow().remainingGoals())
        .containsExactly("customize_burger", "add_sides_drink", "choose_dining");

    assertThat(ShowcaseReviewGoals.talk("showcase_ORDER", ORDER).orElseThrow().hasGoals())
        .isFalse();
  }

  @Test
  void otherSavedSituationsHaveNoConditions() {
    assertThat(ShowcaseReviewGoals.talk(null, List.of())).isEmpty();
    assertThat(ShowcaseReviewGoals.talk("first_day_FIND_BAG_report_bag", List.of())).isEmpty();
    assertThat(ShowcaseReviewGoals.talk("showcase_TAXI", List.of())).isEmpty();
    assertThat(ShowcaseReviewGoals.talk("showcase_UNKNOWN", List.of())).isEmpty();
  }

  @Test
  void cardShowsEachConditionWithItsHint() {
    var goals = ShowcaseReviewGoals.goals("showcase_ORDER");
    assertThat(goals).extracting(goal -> goal.id()).containsExactlyElementsOf(ORDER);
    assertThat(goals.getFirst().title()).isEqualTo("치즈버거 주문하기");
    assertThat(goals.getFirst().hint()).isEqualTo("한 개를 주문해 보세요.");
    assertThat(ShowcaseReviewGoals.goals("first_day_FIND_BAG")).isEmpty();
  }
}
