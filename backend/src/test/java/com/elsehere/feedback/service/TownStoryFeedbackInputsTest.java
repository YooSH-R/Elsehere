package com.elsehere.feedback.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.Scene;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class TownStoryFeedbackInputsTest {
  @Test
  void breakfastOrderFeedbackDoesNotRequireTheLaterSpillOrLaundry() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachTownStoryStage(0);
    conversation.recordTownStoryGoals(List.of("order_breakfast"));

    var input = FeedbackInputs.from(conversation, dinerStaff(), List.of(), UUID.randomUUID());

    assertThat(input.context().mode()).isEqualTo("quest");
    assertThat(input.context().sceneDescription())
        .contains("아침 주문", "음식과 음료")
        .doesNotContain("쏟", "세탁");
  }

  private static Npc dinerStaff() {
    Scene scene = mock(Scene.class);
    when(scene.getCode()).thenReturn("TOWN");
    when(scene.getName()).thenReturn("마을");
    Npc npc = mock(Npc.class);
    when(npc.getScene()).thenReturn(scene);
    when(npc.getCode()).thenReturn("DINER_STAFF");
    when(npc.getName()).thenReturn("Rosa");
    when(npc.getRoleLabel()).thenReturn("식당 종업원");
    return npc;
  }

  @Test
  void spillFeedbackOnlyRequiresExplainingTheSpill() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachTownStoryStage(0);
    conversation.recordTownStoryGoals(List.of("explain_spill"));

    var input = FeedbackInputs.from(conversation, dinerStaff(), List.of(), UUID.randomUUID());

    assertThat(input.context().sceneDescription())
        .contains("쏟은 일 설명", "무엇을 어디에")
        .doesNotContain("아침 주문", "세탁");
  }

  @Test
  void oldDinerConversationDoesNotGuessMissingObjectives() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachTownStoryStage(0);

    var input = FeedbackInputs.from(conversation, dinerStaff(), List.of(), UUID.randomUUID());

    assertThat(input.context().sceneDescription())
        .contains("실제 발화만 평가")
        .doesNotContain("쏟", "세탁", "아침 주문");
  }

  @Test
  void conversationWhileWaitingDoesNotReintroduceCompletedGoals() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachTownStoryStage(0);
    conversation.recordTownStoryGoals(List.of());

    var input = FeedbackInputs.from(conversation, dinerStaff(), List.of(), UUID.randomUUID());

    assertThat(input.context().sceneDescription())
        .contains("할 일: 없음")
        .doesNotContain("쏟", "세탁", "아침 주문");
  }

  @Test
  void feedbackUsesTheStoryStageWhenTheConversationStarted() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachTownStoryStage(5);
    Scene scene = mock(Scene.class);
    when(scene.getCode()).thenReturn("TOWN");
    when(scene.getName()).thenReturn("마을");
    Npc npc = mock(Npc.class);
    when(npc.getScene()).thenReturn(scene);
    when(npc.getCode()).thenReturn("FLOWER_SHOP_STAFF");
    var input = FeedbackInputs.from(conversation, npc, List.of(), UUID.randomUUID());

    assertThat(input.context().mode()).isEqualTo("quest");
    assertThat(input.context().sceneDescription()).contains("꽃", "Tony");
  }
}
