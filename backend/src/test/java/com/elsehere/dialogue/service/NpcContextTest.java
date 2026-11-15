package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import java.util.Map;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class NpcContextTest {

  @Test
  void movementFactsReachTheWorkerWithoutChangingThePerson() {
    NpcContext result =
        ON_DUTY.offDuty().withTravelSituation("travelling to work; walked 35 metres; leave early");
    assertThat(result.workerNpc().scene())
        .contains("travelling to work", "35 metres", "leave early");
    assertThat(result.workerNpc().profile()).isEqualTo(PROFILE);
    assertThat(ON_DUTY.withTravelSituation(null)).isEqualTo(ON_DUTY);
  }

  @Test
  @DisplayName("대화를 시작한 순간의 욕구를 워커 NPC에 싣고, 상황을 바꿔도 그대로 둔다")
  void needsTravelWithTheWorkerNpc() {
    Map<String, Double> needs = Map.of("HUNGER", 35.0);

    NpcContext result = ON_DUTY.withNeeds(needs).withTravelSituation("walking to work");

    assertThat(result.workerNpc().needs()).isEqualTo(needs);
    assertThat(ON_DUTY.withNeeds(null)).isEqualTo(ON_DUTY);
  }

  private static final Map<String, Object> PROFILE = Map.of("name", "Mia");
  private static final NpcContext ON_DUTY =
      new NpcContext(
          2L,
          new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원"),
          "CAFE",
          true,
          new WorkerNpc(
              "CAFE_STAFF",
              "Mia",
              "the barista at the corner cafe.",
              "Menu: latte $4.",
              "The counter of the corner cafe.",
              "af_heart",
              1.0,
              PROFILE),
          "af_heart",
          null,
          null);

  @Test
  @DisplayName("근무가 아닌 NPC는 워커에 보내는 상황을 응대 상황 대신 일 없이 쉬며 잡담하는 일상 상황으로 바꾼다")
  void offDuty_replacesServiceSceneWithOffDutyScene() {
    WorkerNpc npc = ON_DUTY.offDuty().workerNpc();

    assertThat(npc.scene())
        .contains("Mia is off work right now")
        .contains("does not take orders or serve anyone")
        .doesNotContain("counter");
  }

  @Test
  @DisplayName("근무가 아니어도 역할·사실·인격 카드·목소리는 그대로 보내 사적인 화제는 워커의 관계 규칙대로 열린다")
  void offDuty_keepsPersonaAndProfile() {
    NpcContext offDuty = ON_DUTY.offDuty();

    assertThat(offDuty.workerNpc())
        .usingRecursiveComparison()
        .ignoringFields("scene")
        .isEqualTo(ON_DUTY.workerNpc());
    assertThat(offDuty).usingRecursiveComparison().ignoringFields("workerNpc").isEqualTo(ON_DUTY);
  }
}
