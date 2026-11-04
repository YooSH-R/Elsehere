package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.Test;

class MissionDialogueServiceTest {
  @Test
  void activeTownStoryTakesPrecedenceOverCompletedFirstDayContext() {
    FirstDayMissionService firstDay = mock(FirstDayMissionService.class);
    TownStoryService town = mock(TownStoryService.class);
    var townContext =
        new FirstDayMissionService.Context(
            0, new WorkerState("town_story_0", "story", List.of()), "story", List.of());
    when(town.context(8L, "DINER_STAFF")).thenReturn(Optional.of(townContext));

    var selected =
        new MissionDialogueService(firstDay, town).context(8L, "DINER_STAFF").orElseThrow();
    assertThat(selected.townStory()).isTrue();
    assertThat(selected.detail().state().id()).isEqualTo("town_story_0");
  }

  @Test
  void acceptedTownDialogueAdvancesTownProgress() {
    TownStory story = TownStory.start(8L);
    TownStoryRepository stories = mock(TownStoryRepository.class);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));
    TownStoryService town = new TownStoryService(stories, mock(FirstDayMissionRepository.class));
    MissionDialogueService missions =
        new MissionDialogueService(mock(FirstDayMissionService.class), town);
    var context = missions.context(8L, "DINER_STAFF").orElseThrow();
    missions.accept(
        8L,
        "DINER_STAFF",
        context,
        new JudgeResult.Intent(null, List.of("order_breakfast", "explain_spill"), null));
    assertThat(story.getStage()).isEqualTo(1);
  }
}
