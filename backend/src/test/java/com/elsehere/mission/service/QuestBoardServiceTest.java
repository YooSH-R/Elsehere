package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

/**
 * 퀘스트 목록은 첫날 연계 미션과, 마을 이야기 12단계를 네 장으로 나눈 퀘스트 다섯 개다. 앞 퀘스트를 끝내야 다음이 열리고, 끝낸 퀘스트의 보상 코인은 잔액에 더한다.
 */
class QuestBoardServiceTest {
  private final FirstDayMissionRepository firstDay = mock(FirstDayMissionRepository.class);
  private final TownStoryRepository stories = mock(TownStoryRepository.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final QuestBoardService service = new QuestBoardService(firstDay, stories, npcs);

  @BeforeEach
  void knowTheNpcNames() {
    List<Npc> named = List.of(npc("HOTEL_STAFF", "Jake"), npc("BARBER_STAFF", "Tony"));
    when(npcs.findAll()).thenReturn(named);
  }

  @Test
  void beforeStarting_onlyTheFirstQuestIsOpen() {
    when(firstDay.findById(8L)).thenReturn(Optional.empty());
    when(stories.findById(8L)).thenReturn(Optional.empty());

    QuestBoardResponse board = service.board(8L);

    assertThat(board.quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("AVAILABLE", "LOCKED", "LOCKED");
    assertThat(board.quests())
        .extracting(QuestBoardResponse.Quest::reward)
        .containsExactly(10, 15, 10);
    assertThat(board.quests())
        .allSatisfy(quest -> assertThat(quest.title()).doesNotContain("첫날", "여정"));
    assertThat(board.quests().getFirst().stops()).hasSize(7);
    assertThat(board.balance()).isEqualTo(100);
  }

  @Test
  void duringTheFirstQuest_marksVisitedStopsAndWhereToGoNext() {
    FirstDayMission mission = mock(FirstDayMission.class);
    when(mission.getStage()).thenReturn(2);
    when(firstDay.findById(8L)).thenReturn(Optional.of(mission));
    when(stories.findById(8L)).thenReturn(Optional.empty());

    QuestBoardResponse.Quest quest = service.board(8L).quests().getFirst();

    assertThat(quest.status()).isEqualTo("IN_PROGRESS");
    assertThat(quest.stops())
        .extracting(QuestBoardResponse.Stop::done)
        .startsWith(true, true, false);
    QuestBoardResponse.Stop next = quest.stops().get(2);
    assertThat(next.current()).isTrue();
    assertThat(next.place()).isEqualTo("호텔");
    assertThat(next.npcName()).isEqualTo("Jake");
  }

  @Test
  void townStoryChapters_followTheStoryStage_andFinishedRewardsAddToTheBalance() {
    FirstDayMission finished = mock(FirstDayMission.class);
    when(finished.isCompleted()).thenReturn(true);
    when(finished.getStage()).thenReturn(7);
    TownStory story = mock(TownStory.class);
    when(story.getStage()).thenReturn(5);
    when(firstDay.findById(8L)).thenReturn(Optional.of(finished));
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    QuestBoardResponse board = service.board(8L);

    assertThat(board.quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("DONE", "IN_PROGRESS", "LOCKED");
    assertThat(board.quests().get(1).stops())
        .extracting(QuestBoardResponse.Stop::place)
        .containsExactly("식당", "빨래방", "은행", "빨래방", "이발소", "꽃집", "이발소");
    assertThat(board.quests().get(1).stops().get(5).current()).isTrue();
    // 첫 퀘스트 −12 +10, 마을 이야기 5단계까지 아침 7·세탁 6·이발 15코인
    assertThat(board.balance()).isEqualTo(100 - 12 + 10 - 7 - 6 - 15);
  }

  @Test
  void afterTheFirstQuest_theNextOneIsReadyToStart() {
    FirstDayMission finished = mock(FirstDayMission.class);
    when(finished.isCompleted()).thenReturn(true);
    when(finished.getStage()).thenReturn(7);
    when(firstDay.findById(8L)).thenReturn(Optional.of(finished));
    when(stories.findById(8L)).thenReturn(Optional.empty());

    QuestBoardResponse board = service.board(8L);

    assertThat(board.quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("DONE", "AVAILABLE", "LOCKED");
    assertThat(board.balance()).isEqualTo(100 - 12 + 10);
  }

  // 두 번째 퀘스트를 끝내면 이야기가 멈추고 세 번째 퀘스트가 새로 열린다.
  @Test
  void afterTheSecondQuest_theThirdOneIsReadyToStart() {
    FirstDayMission finished = mock(FirstDayMission.class);
    when(finished.isCompleted()).thenReturn(true);
    when(finished.getStage()).thenReturn(7);
    TownStory story = TownStory.start(8L);
    while (story.getStage() < 7) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    when(firstDay.findById(8L)).thenReturn(Optional.of(finished));
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    QuestBoardResponse board = service.board(8L);

    assertThat(board.quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("DONE", "DONE", "AVAILABLE");
    assertThat(board.quests().get(2).stops()).noneMatch(QuestBoardResponse.Stop::current);
  }

  private static Npc npc(String code, String name) {
    Npc npc = mock(Npc.class);
    when(npc.getCode()).thenReturn(code);
    when(npc.getName()).thenReturn(name);
    return npc;
  }
}
