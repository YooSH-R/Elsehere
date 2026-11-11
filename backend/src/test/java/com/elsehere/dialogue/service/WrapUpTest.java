package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.mission.service.QuestTalk;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class WrapUpTest {

  private static final WorkerState CAFE_BREAK =
      new WorkerState(
          "first_day_1",
          "Offer drinks.",
          List.of(
              new WorkerState.Intent("order_drink", "Order a drink."),
              new WorkerState.Intent("set_options", "Answer about options."),
              new WorkerState.Intent("dine_in", "Say they will have it here.")));
  private static final Optional<QuestTalk> FREE_TALK = Optional.empty();

  @Test
  @DisplayName("미션 대화는 이 턴으로 그 NPC에게 남은 목표가 모두 끝나면 마무리한다")
  void ends_missionGoalsDoneThisTurn() {
    assertThat(WrapUp.ends(false, mission("dine_in"), judged(List.of("dine_in")))).isTrue();
  }

  @Test
  @DisplayName("미션 목표가 남으면 용건 끝냄이라도 이어 간다")
  void ends_missionGoalsLeft_keepsGoing() {
    assertThat(
            WrapUp.ends(false, mission("set_options", "dine_in"), judged(List.of("set_options"))))
        .isFalse();
    assertThat(WrapUp.ends(false, mission("dine_in"), judged(List.of(), "TASK_DONE"))).isFalse();
  }

  @Test
  @DisplayName("미션 목표가 남아도 학습자가 작별하면 마무리한다(다시 말을 걸어 이어 할 수 있음)")
  void ends_farewellDuringMission() {
    assertThat(WrapUp.ends(false, mission("set_options", "dine_in"), judged(List.of(), "LEAVE")))
        .isTrue();
  }

  @ParameterizedTest
  @ValueSource(strings = {"LEAVE", "TASK_DONE"})
  @DisplayName("그 밖의 대화는 학습자가 작별하거나 용건을 끝냈다고 하면 마무리한다")
  void ends_farewellOrTaskDoneInFreeTalk(String event) {
    assertThat(WrapUp.ends(false, FREE_TALK, judged(List.of(), event))).isTrue();
  }

  @Test
  @DisplayName("미션 맥락이 있어도 이 NPC에게 남은 목표가 없으면(길 안내만 하는 NPC 등) 그 밖의 대화처럼 정한다")
  void ends_missionWithoutGoalsForThisNpc_likeFreeTalk() {
    Optional<QuestTalk> directionsOnly =
        Optional.of(new QuestTalk.Guide(1, "Help with directions."));

    assertThat(WrapUp.ends(false, directionsOnly, judged(List.of(), "TASK_DONE"))).isTrue();
    assertThat(WrapUp.ends(false, directionsOnly, judged(List.of(), "GREETING"))).isFalse();
  }

  @Test
  @DisplayName("작별·용건 끝냄이 없는 평범한 줄은 이어 간다")
  void ends_ordinaryLine_keepsGoing() {
    assertThat(WrapUp.ends(false, FREE_TALK, judged(List.of(), "GREETING", "SMALL_TALK")))
        .isFalse();
  }

  @Test
  @DisplayName("무례로 센 턴은 무례 처리만 하고 마무리하지 않는다")
  void ends_rudeTurn_keepsGoing() {
    assertThat(WrapUp.ends(true, FREE_TALK, judged(List.of(), "LEAVE"))).isFalse();
    assertThat(WrapUp.ends(true, mission("dine_in"), judged(List.of("dine_in")))).isFalse();
  }

  @Test
  @DisplayName("사건을 보내지 않는 이전 워커나 의도 판정 실패면 이어 간다")
  void ends_unknownEventsOrFailedIntent_keepsGoing() {
    JudgeResult withoutEvents =
        new JudgeResult(
            "Bye", false, null, null, List.of(), new JudgeResult.Intent("chat", null, null));
    JudgeResult failedIntent =
        new JudgeResult(
            "Here",
            false,
            null,
            null,
            List.of(),
            new JudgeResult.Intent("unclear", null, "timeout"));

    assertThat(WrapUp.ends(false, FREE_TALK, withoutEvents)).isFalse();
    assertThat(WrapUp.ends(false, mission("dine_in"), failedIntent)).isFalse();
  }

  @Test
  @DisplayName("마무리 지시문은 이번 대답의 지시문 뒤에 붙는다")
  void instruction_appendsTheWrapUpLine() {
    assertThat(WrapUp.instruction("Offer drinks."))
        .isEqualTo("Offer drinks. " + WrapUp.INSTRUCTION);
    assertThat(WrapUp.instruction("")).isEqualTo(WrapUp.INSTRUCTION);
  }

  private static Optional<QuestTalk> mission(String... remaining) {
    return Optional.of(new QuestTalk.Goals(1, CAFE_BREAK, "Offer drinks.", List.of(remaining)));
  }

  private static JudgeResult judged(List<String> intents, String... events) {
    return new JudgeResult(
        "text",
        false,
        null,
        null,
        List.of(),
        new JudgeResult.Intent(intents.isEmpty() ? "chat" : intents.get(0), intents, null),
        List.of(events),
        null);
  }
}
