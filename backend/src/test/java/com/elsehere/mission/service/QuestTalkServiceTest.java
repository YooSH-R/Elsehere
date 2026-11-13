package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

// 대화의 퀘스트 대화 목표를 고르는 창구. 첫날 미션·마을 이야기는 Mock, 장소 연습은 실제 상황 데이터로 본다.
class QuestTalkServiceTest {

  private static final QuestTalk TOWN_GOALS =
      new QuestTalk.Goals(
          0, new WorkerState("town_story_0", "story", List.of()), "story", List.of());
  private static final QuestTalk FIRST_DAY_GUIDE = new QuestTalk.Guide(7, "journey complete");

  private final FirstDayMissionService firstDay = mock(FirstDayMissionService.class);
  private final TownStoryService town = mock(TownStoryService.class);
  private final PlacePracticeService practice =
      new PlacePracticeService(
          mock(ConversationRepository.class),
          mock(NpcRepository.class),
          Clock.fixed(Instant.EPOCH, ZoneOffset.UTC));
  private final ShowcaseQuestService showcase = mock(ShowcaseQuestService.class);
  private final QuestTalkService quests = new QuestTalkService(firstDay, town, practice, showcase);

  @Test
  @DisplayName("시연 서버에서 시연을 시작하기 전에는 진행 중이던 다른 퀘스트도 대화 목표를 내지 않는다")
  void onTheShowcaseServer_otherQuestsGiveNoGoalsBeforeTheDemoStarts() {
    when(showcase.enabled()).thenReturn(true);
    when(showcase.context(8L, "HOTEL_STAFF")).thenReturn(Optional.empty());
    when(town.context(8L, "HOTEL_STAFF")).thenReturn(Optional.of(TOWN_GOALS));
    when(firstDay.context(8L, "HOTEL_STAFF")).thenReturn(Optional.of(FIRST_DAY_GUIDE));

    assertThat(quests.forStart(8L, "HOTEL_STAFF", false, null)).isEmpty();
  }

  @Test
  @DisplayName("마을 이야기를 하는 중이면 첫날 미션보다 마을 이야기의 대화 목표를 고른다")
  void activeTownStoryTakesPrecedenceOverFirstDay() {
    when(town.context(8L, "DINER_STAFF")).thenReturn(Optional.of(TOWN_GOALS));
    when(firstDay.context(8L, "DINER_STAFF")).thenReturn(Optional.of(FIRST_DAY_GUIDE));

    var selected = quests.forStart(8L, "DINER_STAFF", false, null).orElseThrow();

    assertThat(selected.source()).isEqualTo(QuestTalkService.Source.TOWN_STORY);
    assertThat(selected.talk()).isSameAs(TOWN_GOALS);
  }

  @Test
  @DisplayName("마을 이야기를 하지 않으면 첫날 미션의 대화 목표를 고른다")
  void withoutTownStory_usesTheFirstDayMission() {
    when(town.context(8L, "DINER_STAFF")).thenReturn(Optional.empty());
    when(firstDay.context(8L, "DINER_STAFF")).thenReturn(Optional.of(FIRST_DAY_GUIDE));

    var selected = quests.forStart(8L, "DINER_STAFF", false, null).orElseThrow();

    assertThat(selected.source()).isEqualTo(QuestTalkService.Source.FIRST_DAY);
    assertThat(selected.talk()).isSameAs(FIRST_DAY_GUIDE);
  }

  @Test
  @DisplayName("장소 연습을 시작하면 진행 중인 퀘스트 대신 고른 상황의 목표를 아직 이룬 것 없이 고른다")
  void startingPractice_usesTheChosenScenario() {
    when(town.context(8L, "HOTEL_STAFF")).thenReturn(Optional.of(TOWN_GOALS));
    PracticeScenario scenario = practice.scenario("HOTEL_CHANGE_ROOM");

    var selected = quests.forStart(8L, "HOTEL_STAFF", false, scenario).orElseThrow();

    assertThat(selected.source()).isEqualTo(QuestTalkService.Source.PRACTICE);
    assertThat(selected.talk()).isEqualTo(practice.context(scenario, List.of()));
  }

  @Test
  @DisplayName("복습 대화는 시작할 때도 발화할 때도 퀘스트 대화 목표가 없다")
  void review_hasNoQuestTalk() {
    when(town.context(8L, "DINER_STAFF")).thenReturn(Optional.of(TOWN_GOALS));
    Conversation review = conversation();
    review.attachReviewCard(5L);

    assertThat(quests.forStart(8L, "DINER_STAFF", true, null)).isEmpty();
    assertThat(quests.forTurn(8L, "DINER_STAFF", review)).isEmpty();
  }

  @Test
  @DisplayName("장소 연습 대화에서 발화하면 진행 중인 퀘스트 대신 그 대화에서 고른 상황의 목표를 고른다")
  void practiceTurn_usesTheConversationsScenario() {
    when(town.context(8L, "HOTEL_STAFF")).thenReturn(Optional.of(TOWN_GOALS));
    Conversation conversation = practiceConversation();

    var selected = quests.forTurn(8L, "HOTEL_STAFF", conversation).orElseThrow();

    assertThat(selected.source()).isEqualTo(QuestTalkService.Source.PRACTICE);
    assertThat(selected.talk()).isEqualTo(practice.context(conversation).orElseThrow());
  }

  @Test
  @DisplayName("장소 연습에서 판정이 알아본 목표는 그 연습 대화에 남는다")
  void acceptPractice_recordsTheGoalOnThatConversation() {
    Conversation conversation = practiceConversation();
    var selected = quests.forTurn(8L, "HOTEL_STAFF", conversation).orElseThrow();

    quests.accept(
        8L,
        "HOTEL_STAFF",
        conversation,
        selected,
        new JudgeResult.Intent(null, List.of("describe_room_problem"), null));

    assertThat(conversation.getPracticeCompletedGoals()).containsExactly("describe_room_problem");
  }

  @Test
  @DisplayName("마을 이야기에서 판정이 알아본 목표는 마을 이야기 진행에 남는다")
  void acceptTownStory_advancesTownProgress() {
    TownStory story = TownStory.start(8L);
    TownStoryRepository stories = mock(TownStoryRepository.class);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));
    TownStoryService realTown =
        new TownStoryService(
            stories,
            mock(FirstDayMissionRepository.class),
            mock(NpcRepository.class),
            mock(ConversationRepository.class));
    QuestTalkService withRealTown =
        new QuestTalkService(
            firstDay,
            realTown,
            practice,
            mock(com.elsehere.mission.service.ShowcaseQuestService.class));
    var selected = withRealTown.forStart(8L, "DINER_STAFF", false, null).orElseThrow();

    withRealTown.accept(
        8L,
        "DINER_STAFF",
        conversation(),
        selected,
        new JudgeResult.Intent(null, List.of("order_breakfast", "explain_spill"), null));

    assertThat(story.getStage()).isZero();
    assertThat(story.hasGoal(0)).isTrue();
    assertThat(story.hasGoal(1)).isFalse();
  }

  @Test
  @DisplayName("판정할 목표가 있는 첫날 미션 대화는 대화에 그 단계와 남은 목표를 남기고, 안내만 하는 대화는 남기지 않는다")
  void recordStage_onlyForGoals() {
    QuestTalk goals =
        new QuestTalk.Goals(
            2, new WorkerState("first_day_2", "bag", List.of()), "bag", List.of("report_bag"));
    Conversation withGoals = conversation();
    Conversation withGuide = conversation();

    new QuestTalkService.SelectedQuest(QuestTalkService.Source.FIRST_DAY, goals)
        .recordStage(withGoals);
    new QuestTalkService.SelectedQuest(QuestTalkService.Source.FIRST_DAY, FIRST_DAY_GUIDE)
        .recordStage(withGuide);

    assertThat(withGoals.getFirstDayStage()).isEqualTo(2);
    assertThat(withGoals.getFirstDayGoalIds()).containsExactly("report_bag");
    assertThat(withGuide.getFirstDayStage()).isNull();
  }

  private static Conversation conversation() {
    return Conversation.start(8L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
  }

  private static Conversation practiceConversation() {
    Conversation conversation = conversation();
    conversation.attachPracticeScenario("HOTEL_CHANGE_ROOM");
    return conversation;
  }
}
