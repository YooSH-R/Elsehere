package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

/** 퀘스트 그만두기. 하던 퀘스트를 그 퀘스트의 처음으로 되돌리고, 끝낸 퀘스트와 받은 보상은 그대로 둔다. */
class QuestCancelServiceTest {
  private final UserRepository users = mock(UserRepository.class);
  private final ConversationRepository conversations = mock(ConversationRepository.class);
  private final FirstDayMissionRepository firstDay = mock(FirstDayMissionRepository.class);
  private final TownStoryRepository stories = mock(TownStoryRepository.class);
  private final ShowcaseJourneyRepository journeys = mock(ShowcaseJourneyRepository.class);
  private final ShowcaseScene scene = mock(ShowcaseScene.class);
  private final QuestCancelService service =
      new QuestCancelService(
          users,
          conversations,
          firstDay,
          stories,
          new ShowcaseQuestService(
              new ShowcaseSettings(false),
              journeys,
              users,
              conversations,
              scene,
              firstDay,
              stories));

  @BeforeEach
  void signedIn() {
    when(users.findForUpdate(8L)).thenReturn(Optional.of(mock(User.class)));
  }

  // 하던 첫날 퀘스트를 그만두면 진행 행을 지워 시작 전으로 돌아간다. 그 안에서 쓴 코인은 진행에서 계산하므로 함께 돌아온다.
  @Test
  void cancellingTheFirstDayQuestReturnsItToBeforeTheStart() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, FirstDayStage.values()[0].npc(), FirstDayStage.values()[0].intentIds());
    when(firstDay.findForUpdate(8L)).thenReturn(Optional.of(mission));

    service.cancel(8L, "CHECK_IN");

    verify(firstDay).delete(mission);
  }

  // 하던 마을 퀘스트를 그만두면 그 퀘스트의 첫 단계로 돌아가 목록에서 다시 시작하기를 기다린다. 앞서 끝낸 퀘스트는 그대로다.
  @Test
  void cancellingATownQuestReturnsToItsFirstStageAndKeepsTheEarlierQuest() {
    TownStory story = storyAt(9);
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));

    service.cancel(8L, "THANKS_AND_PARCEL");

    assertThat(story.getStage()).isEqualTo(7);
    assertThat(story.isPaused()).isTrue();
  }

  // 하던 시연을 그만두면 처음으로 돌아가고, 시연이 덮어쓴 NPC 관계·게임 시각도 시작 전으로 돌려놓는다.
  @Test
  void cancellingTheShowcaseRestartsItAndPutsTheAccountBack() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(journey));

    service.cancel(8L, ShowcaseStage.QUEST_ID);

    assertThat(journey.getStage()).isZero();
    assertThat(journey.isActive()).isFalse();
    verify(scene).leave(8L);
  }

  // 진행 중이 아닌 퀘스트(시작 전·끝냄·다음 퀘스트 시작을 기다림·다른 퀘스트를 하는 중)는 그만둘 수 없고, 진행도 그대로다.
  @Test
  void aQuestThatIsNotInProgressCannotBeCancelled() {
    TownStory morning = storyAt(2);
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(morning));
    when(firstDay.findForUpdate(8L)).thenReturn(Optional.empty());
    ShowcaseJourney finished = ShowcaseJourney.start(8L);
    finished.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    finished.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    finished.arriveByTaxi("DINER");
    finished.accept(2, "DINER_STAFF", ShowcaseStage.ORDER.goalIds());
    when(journeys.findForUpdate(8L)).thenReturn(Optional.of(finished));

    assertNotInProgress("THANKS_AND_PARCEL");
    assertNotInProgress("CHECK_IN");
    assertNotInProgress(ShowcaseStage.QUEST_ID);

    when(stories.findForUpdate(8L)).thenReturn(Optional.of(storyAt(7)));
    assertNotInProgress("THANKS_AND_PARCEL");

    assertThat(morning.getStage()).isEqualTo(2);
    assertThat(finished.isCompleted()).isTrue();
    verifyNoInteractions(scene);
  }

  // 대화 중에는 그 대화가 진행을 바꿀 수 있어 그만두지 않는다.
  @Test
  void aQuestCannotBeCancelledDuringAConversation() {
    FirstDayMission mission = FirstDayMission.start(8L);
    when(firstDay.findForUpdate(8L)).thenReturn(Optional.of(mission));
    when(conversations.findFirstByUserIdAndStatus(8L, ConversationStatus.ACTIVE))
        .thenReturn(Optional.of(mock(Conversation.class)));

    assertThatThrownBy(() -> service.cancel(8L, "CHECK_IN"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error ->
                assertThat(error.getErrorCode()).isEqualTo(ErrorCode.CONVERSATION_ALREADY_ACTIVE));
    verify(firstDay, never()).delete(any());
  }

  @Test
  void anUnknownQuestIsNotFound() {
    assertThatThrownBy(() -> service.cancel(8L, "NO_SUCH_QUEST"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  private void assertNotInProgress(String questId) {
    assertThatThrownBy(() -> service.cancel(8L, questId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.QUEST_NOT_IN_PROGRESS));
  }

  // 마을 이야기를 stage 단계까지 진행한다. 퀘스트 하나를 끝내 멈추면 stage가 그 다음 퀘스트의 첫 단계일 때만 멈춘 채 둔다.
  private static TownStory storyAt(int stage) {
    TownStory story = TownStory.start(8L);
    while (story.getStage() < stage) {
      story.resume();
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    return story;
  }
}
