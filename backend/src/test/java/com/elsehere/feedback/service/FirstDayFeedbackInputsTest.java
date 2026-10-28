package com.elsehere.feedback.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.FeedbackInput;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.Scene;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class FirstDayFeedbackInputsTest {
  @Test
  void feedbackUsesConversationStageEvenAfterMissionHasAdvanced() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.attachFirstDayStage(3);
    Scene scene = mock(Scene.class);
    when(scene.getCode()).thenReturn("CAFE");
    when(scene.getName()).thenReturn("카페");
    Npc npc = mock(Npc.class);
    when(npc.getScene()).thenReturn(scene);
    when(npc.getCode()).thenReturn("CAFE_STAFF");
    FeedbackInput input = FeedbackInputs.from(conversation, npc, List.of(), UUID.randomUUID());
    assertThat(input.context().mode()).isEqualTo("quest");
    assertThat(input.context().sceneDescription()).contains("가방", "분실물");
  }
}
