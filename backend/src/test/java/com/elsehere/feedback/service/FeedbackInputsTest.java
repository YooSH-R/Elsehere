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
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class FeedbackInputsTest {

  @Test
  @DisplayName("중급 대화는 워커 힌트와 같은 중급 학습자 수준(CEFR B1)으로 피드백을 맞춘다")
  void difficulty_intermediate_describesB1Learner() {
    FeedbackInput.Difficulty difficulty = FeedbackInputs.difficulty(2);

    assertThat(difficulty.vocabularyProfile())
        .isEqualTo("level-2: natural everyday English (CEFR B1)");
  }

  @Test
  @DisplayName("근무 중인 NPC와의 자유 대화는 장소·역할·이름 뒤에 자유 대화라고 적는다")
  void from_onDutyConversation_describesFreeChat() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);

    FeedbackInput input =
        FeedbackInputs.from(conversation, cafeStaff(), List.of(), UUID.randomUUID());

    assertThat(input.context().sceneDescription()).isEqualTo("모퉁이 카페 · 카페 직원 Mia · 자유 대화");
  }

  @Test
  @DisplayName("근무가 아닌 NPC와의 대화는 응대가 아닌 일상 대화라고 적어 평가가 응대 상황을 기대하지 않게 한다")
  void from_offDutyConversation_describesEverydayTalk() {
    Conversation conversation = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    conversation.markNpcOffDuty();

    FeedbackInput input =
        FeedbackInputs.from(conversation, cafeStaff(), List.of(), UUID.randomUUID());

    assertThat(input.context().mode()).isEqualTo("free_chat");
    assertThat(input.context().sceneDescription())
        .isEqualTo("모퉁이 카페 · 카페 직원 Mia · 근무가 아닐 때의 일상 대화");
  }

  private static Npc cafeStaff() {
    Scene scene = mock(Scene.class);
    when(scene.getCode()).thenReturn("CAFE");
    when(scene.getName()).thenReturn("모퉁이 카페");
    Npc npc = mock(Npc.class);
    when(npc.getScene()).thenReturn(scene);
    when(npc.getCode()).thenReturn("CAFE_STAFF");
    when(npc.getName()).thenReturn("Mia");
    when(npc.getRoleLabel()).thenReturn("카페 직원");
    return npc;
  }
}
