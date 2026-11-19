package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.favor.repository.FavorRepository;
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
  private final FavorRepository favors = mock(FavorRepository.class);
  private final ShowcaseQuestService showcase = mock(ShowcaseQuestService.class);
  private final QuestBoardService service =
      new QuestBoardService(firstDay, stories, npcs, favors, showcase);

  @BeforeEach
  void knowTheNpcNames() {
    List<Npc> named = List.of(npc("HOTEL_STAFF", "Jake"), npc("BARBER_STAFF", "Tony"));
    when(npcs.findAll()).thenReturn(named);
  }

  @Test
  void theShowcaseQuestComesFirstInTheStoryList() {
    when(firstDay.findById(8L)).thenReturn(Optional.empty());
    when(stories.findById(8L)).thenReturn(Optional.empty());
    var card =
        new QuestBoardResponse.Quest(
            "SHOWCASE_RAINY_EVENING", "비 오는 저녁, 햄버거 한 끼", "", 0, "AVAILABLE", List.of());
    when(showcase.card(org.mockito.ArgumentMatchers.eq(8L), org.mockito.ArgumentMatchers.any()))
        .thenReturn(Optional.of(card));

    QuestBoardResponse board = service.board(8L);

    assertThat(board.quests())
        .extracting(QuestBoardResponse.Quest::id)
        .startsWith("SHOWCASE_RAINY_EVENING", "CHECK_IN");
  }

  // 시연 중에는 맨 앞의 시연 퀘스트만 열고, 시작하지 않은 나머지는 잠근다. 시연을 끝내면 원래 규칙으로 돌아간다.
  @Test
  void whileTheShowcaseIsNotDone_onlyItIsOpenAndTheRestAreLocked() {
    // 시연 전용 서버: 시연 카드가 끝나기 전까지 다른 퀘스트를 잠근다.
    when(showcase.locksOtherQuests(org.mockito.ArgumentMatchers.any()))
        .thenAnswer(call -> !"DONE".equals(call.<QuestBoardResponse.Quest>getArgument(0).status()));
    when(firstDay.findById(8L)).thenReturn(Optional.empty());
    when(stories.findById(8L)).thenReturn(Optional.empty());
    when(showcase.card(org.mockito.ArgumentMatchers.eq(8L), org.mockito.ArgumentMatchers.any()))
        .thenReturn(Optional.of(showcaseCard("AVAILABLE")));

    assertThat(service.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("AVAILABLE", "LOCKED", "LOCKED", "LOCKED");

    when(showcase.card(org.mockito.ArgumentMatchers.eq(8L), org.mockito.ArgumentMatchers.any()))
        .thenReturn(Optional.of(showcaseCard("DONE")));

    assertThat(service.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("DONE", "AVAILABLE", "LOCKED", "LOCKED");
  }

  // 시연 서버에서는 진행 중이던 다른 퀘스트도 잠가, 시연 퀘스트만 눌러 시작할 수 있다.
  @Test
  void onTheShowcaseServer_aQuestAlreadyInProgressIsLockedToo() {
    // 시연 전용 서버: 시연 카드가 끝나기 전까지 다른 퀘스트를 잠근다.
    when(showcase.locksOtherQuests(org.mockito.ArgumentMatchers.any()))
        .thenAnswer(call -> !"DONE".equals(call.<QuestBoardResponse.Quest>getArgument(0).status()));
    FirstDayMission mission = mock(FirstDayMission.class);
    when(mission.getStage()).thenReturn(2);
    when(firstDay.findById(8L)).thenReturn(Optional.of(mission));
    when(stories.findById(8L)).thenReturn(Optional.empty());
    when(showcase.card(org.mockito.ArgumentMatchers.eq(8L), org.mockito.ArgumentMatchers.any()))
        .thenReturn(Optional.of(showcaseCard("AVAILABLE")));

    assertThat(service.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::status)
        .containsExactly("AVAILABLE", "LOCKED", "LOCKED", "LOCKED");
  }

  // 잠긴 퀘스트는 왜 잠겼는지 함께 준다. 어느 퀘스트를 마쳐야 하는지 앞 퀘스트 이름으로 알린다. 열린 퀘스트에는 이유가 없다.
  @Test
  void aQuestLockedUntilThePreviousOneIsDoneNamesThatQuest() {
    when(firstDay.findById(8L)).thenReturn(Optional.empty());
    when(stories.findById(8L)).thenReturn(Optional.empty());

    assertThat(service.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::lockReason)
        .containsExactly(null, "'호텔 체크인과 잃어버린 가방' 퀘스트를 마치면 열려요.", "'옷에 쏟은 아침과 새 머리' 퀘스트를 마치면 열려요.");
  }

  // 시연 때문에 잠긴 퀘스트는 시연이 잠갔다고 알린다. 원래 앞 퀘스트 때문에 잠긴 퀘스트는 그 이유를 그대로 둔다.
  @Test
  void aQuestLockedByTheShowcaseSaysTheShowcaseLockedIt() {
    when(showcase.locksOtherQuests(org.mockito.ArgumentMatchers.any())).thenReturn(true);
    when(showcase.lockReason()).thenReturn("'비 오는 저녁, 햄버거 한 끼' 퀘스트를 하는 중이라 잠겼어요.");
    when(firstDay.findById(8L)).thenReturn(Optional.empty());
    when(stories.findById(8L)).thenReturn(Optional.empty());
    when(showcase.card(org.mockito.ArgumentMatchers.eq(8L), org.mockito.ArgumentMatchers.any()))
        .thenReturn(Optional.of(showcaseCard("IN_PROGRESS")));

    assertThat(service.board(8L).quests())
        .extracting(QuestBoardResponse.Quest::lockReason)
        .containsExactly(
            null,
            "'비 오는 저녁, 햄버거 한 끼' 퀘스트를 하는 중이라 잠겼어요.",
            "'호텔 체크인과 잃어버린 가방' 퀘스트를 마치면 열려요.",
            "'옷에 쏟은 아침과 새 머리' 퀘스트를 마치면 열려요.");
  }

  private static QuestBoardResponse.Quest showcaseCard(String status) {
    return new QuestBoardResponse.Quest(
        "SHOWCASE_RAINY_EVENING", "비 오는 저녁, 햄버거 한 끼", "", 0, status, List.of());
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
  void finishedFavorRewardsAreAddedToTheBalance() {
    when(firstDay.findById(8L)).thenReturn(Optional.empty());
    when(stories.findById(8L)).thenReturn(Optional.empty());
    when(favors.sumDoneRewardCoins(8L)).thenReturn(11L);

    assertThat(service.board(8L).balance()).isEqualTo(111);
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
        .containsExactly("햄버거 가게", "빨래방", "은행", "빨래방", "이발소", "꽃집", "이발소");
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
