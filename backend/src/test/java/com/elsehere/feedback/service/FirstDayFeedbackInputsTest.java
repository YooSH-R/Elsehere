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

  // 모델이 장면 목표를 알아야 그 단계에 맞는 표현을 권한다. 카페 단계에 "to go"를 권하는 식의 어긋남을 막는다.
  @Test
  void sceneDescriptionListsTheStageGoals() {
    FeedbackInput input = inputFor(0, 1);

    assertThat(input.context().sceneDescription()).contains("예약 확인", "캐리어 맡기기", "카페 위치 묻기");
  }

  // "level-1" 같은 번호만으로는 모델이 수준에 맞는 길이를 정할 수 없어 수준 설명을 함께 보낸다.
  @Test
  void vocabularyProfileDescribesTheLearnerLevel() {
    FeedbackInput input = inputFor(0, 1);

    assertThat(input.turns()).isEmpty();
    assertThat(input.context().sceneId()).isEqualTo("HOTEL");
    assertThat(FeedbackInputs.difficulty(1).vocabularyProfile()).contains("level-1", "CEFR A1-A2");
    assertThat(FeedbackInputs.difficulty(3).vocabularyProfile()).contains("CEFR B1-B2");
  }

  private static FeedbackInput inputFor(int stage, int level) {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, level, Instant.EPOCH);
    conversation.attachFirstDayStage(stage);
    Scene scene = mock(Scene.class);
    when(scene.getCode()).thenReturn("HOTEL");
    when(scene.getName()).thenReturn("호텔");
    Npc npc = mock(Npc.class);
    when(npc.getScene()).thenReturn(scene);
    when(npc.getCode()).thenReturn("HOTEL_STAFF");
    return FeedbackInputs.from(conversation, npc, List.of(), UUID.randomUUID());
  }
}
