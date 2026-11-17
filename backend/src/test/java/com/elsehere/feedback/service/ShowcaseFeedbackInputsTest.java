package com.elsehere.feedback.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.Scene;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class ShowcaseFeedbackInputsTest {
  @Test
  void orderFeedbackUsesTheRecordedShowcaseSituationWithoutInventingASpill() {
    Conversation source = Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
    source.attachShowcaseStage(ShowcaseStage.ORDER.ordinal());
    source.recordShowcaseGoals(ShowcaseStage.ORDER.goalIds());
    Scene scene = mock(Scene.class);
    when(scene.getCode()).thenReturn("TOWN");
    Npc npc = mock(Npc.class);
    when(npc.getCode()).thenReturn("DINER_STAFF");
    when(npc.getScene()).thenReturn(scene);
    var input = FeedbackInputs.from(source, npc, List.of(), UUID.randomUUID());
    assertThat(input.context().mode()).isEqualTo("quest");
    assertThat(input.context().sceneDescription()).contains("시연", "햄버거", "양파").doesNotContain("세탁");
  }
}
