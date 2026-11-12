package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.Test;

class ShowcaseQuestRoutingTest {
  @Test
  void questBoardShowsShowcaseBalanceWhileActive() {
    ShowcaseQuestService showcase = mock(ShowcaseQuestService.class);
    NpcRepository npcs = mock(NpcRepository.class);
    when(npcs.findAll()).thenReturn(List.of());
    when(showcase.activeStatus(8L))
        .thenReturn(
            Optional.of(
                new com.elsehere.mission.dto.FirstDayMissionResponse(
                    true,
                    3,
                    ShowcaseStage.QUEST_ID,
                    false,
                    "TOWN",
                    false,
                    false,
                    "CLEAR",
                    93,
                    "햄버거 세트",
                    "호텔로 돌아가세요",
                    "호텔로 돌아가세요",
                    List.of())));
    QuestBoardService board =
        new QuestBoardService(
            mock(FirstDayMissionRepository.class), mock(TownStoryRepository.class), npcs, showcase);
    assertThat(board.board(8L).balance()).isEqualTo(93);
  }

  @Test
  void activeShowcaseOverridesNormalQuestButNotReview() {
    ShowcaseQuestService showcase = mock(ShowcaseQuestService.class);
    QuestTalk.Guide guide = new QuestTalk.Guide(1, "Taxi directions");
    when(showcase.context(8L, "TAXI_DRIVER")).thenReturn(Optional.of(guide));
    QuestTalkService service =
        new QuestTalkService(
            mock(FirstDayMissionService.class),
            mock(TownStoryService.class),
            mock(PlacePracticeService.class),
            showcase);
    assertThat(service.forStart(8L, "TAXI_DRIVER", false, null).orElseThrow().source().name())
        .isEqualTo("SHOWCASE");
    assertThat(service.forStart(8L, "TAXI_DRIVER", true, null)).isEmpty();
  }

  @Test
  void enabledShowcaseAddsItsOwnCardWithoutReplacingTheNormalQuests() {
    ShowcaseQuestService showcase = mock(ShowcaseQuestService.class);
    NpcRepository npcs = mock(NpcRepository.class);
    when(npcs.findAll()).thenReturn(List.of());
    when(showcase.card(8L, java.util.Map.of()))
        .thenReturn(
            Optional.of(
                new QuestBoardResponse.Quest(
                    ShowcaseStage.QUEST_ID, "비 오는 저녁, 햄버거 한 끼", "시연", 0, "AVAILABLE", List.of())));
    QuestBoardService board =
        new QuestBoardService(
            mock(FirstDayMissionRepository.class), mock(TownStoryRepository.class), npcs, showcase);
    assertThat(board.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::id)
        .contains("CHECK_IN", ShowcaseStage.QUEST_ID);
  }
}
