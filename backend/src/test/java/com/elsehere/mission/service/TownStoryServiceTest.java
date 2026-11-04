package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.common.error.BusinessException;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class TownStoryServiceTest {
  private final FirstDayMissionRepository firstDay = mock(FirstDayMissionRepository.class);
  private final TownStoryRepository stories = mock(TownStoryRepository.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final TownStoryService service = new TownStoryService(stories, firstDay, npcs);

  @BeforeEach
  void nameTheDinerStaff() {
    Npc rosa = mock(Npc.class);
    when(rosa.getName()).thenReturn("Rosa");
    when(rosa.getRoleLabel()).thenReturn("식당 종업원");
    when(npcs.findByCode("DINER_STAFF")).thenReturn(Optional.of(rosa));
  }

  // 지금 이야기 담당이 아닌 직원에게 물으면 지금 만날 직원과 가게를 알려 준다.
  @Test
  void otherStaffSendTheVisitorToTheCurrentStaff() {
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    FirstDayMissionService.Context bakery = service.context(8L, "BAKERY_STAFF").orElseThrow();

    assertThat(bakery.instruction())
        .contains("the diner", "talk to Rosa", "instead of saying you do not know");
  }

  // 퀘스트 카드가 지금 만날 직원의 이름을 보인다. 마을 밖에 있으면 마을로 나가 만날 사람을 알린다.
  @Test
  void cardNamesTheStaffToTalkTo() {
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));
    FirstDayMission inTown = mock(FirstDayMission.class);
    when(inTown.getScene()).thenReturn("TOWN");
    FirstDayMission inHotel = mock(FirstDayMission.class);
    when(inHotel.getScene()).thenReturn("HOTEL");

    when(firstDay.findById(8L)).thenReturn(Optional.of(inTown));
    assertThat(service.status(8L).direction()).isEqualTo("식당 종업원 Rosa에게 이야기해 보세요.");
    when(firstDay.findById(8L)).thenReturn(Optional.of(inHotel));
    assertThat(service.status(8L).situation()).isEqualTo("마을로 나가 식당 종업원 Rosa에게 이야기해 보세요.");
  }

  @Test
  void cannotStartBeforeFirstDayIsCompleted() {
    when(firstDay.findById(8L)).thenReturn(Optional.of(FirstDayMission.start(8L)));

    assertThatThrownBy(() -> service.start(8L)).isInstanceOf(BusinessException.class);
  }

  @Test
  void restartingAfterCompletionOfFirstDayKeepsExistingTownProgress() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.isCompleted()).thenReturn(true);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    TownStory existing = TownStory.start(8L);
    existing.accept(0, "DINER_STAFF", java.util.List.of("order_breakfast", "explain_spill"));
    when(stories.findById(8L)).thenReturn(Optional.of(existing));

    assertThat(service.start(8L).getStage()).isEqualTo(1);
  }

  @Test
  void firstStartLocksTheExistingFirstDayRecordBeforeCreatingProgress() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.isCompleted()).thenReturn(true);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    when(firstDay.findForUpdate(8L)).thenReturn(Optional.of(completed));
    when(stories.save(any())).thenAnswer(call -> call.getArgument(0));

    assertThat(service.start(8L).getStage()).isZero();
    verify(firstDay).findForUpdate(8L);
  }

  @Test
  void failedJudgementDoesNotAdvanceButAcceptedObjectivesDo() {
    TownStory story = TownStory.start(8L);
    when(stories.findForUpdate(8L)).thenReturn(Optional.of(story));

    service.accept(8L, 0, "DINER_STAFF", new JudgeResult.Intent(null, null, "timeout"));
    assertThat(story.getStage()).isZero();
    service.accept(
        8L,
        0,
        "DINER_STAFF",
        new JudgeResult.Intent(null, java.util.List.of("order_breakfast", "explain_spill"), null));
    assertThat(story.getStage()).isEqualTo(1);
  }

  @Test
  void onlyTheCurrentStaffReceivesTheStoryObjectives() {
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    FirstDayMissionService.Context diner = service.context(8L, "DINER_STAFF").orElseThrow();
    FirstDayMissionService.Context bank = service.context(8L, "BANK_STAFF").orElseThrow();
    assertThat(diner.state().intents())
        .extracting(intent -> intent.id())
        .containsExactly("order_breakfast", "explain_spill");
    assertThat(diner.state().intents().get(1).desc()).containsIgnoringCase("clothes");
    assertThat(diner.state().desc()).containsIgnoringCase("spill");
    assertThat(diner.instruction()).containsIgnoringCase("spill");
    assertThat(bank.state()).isNull();
  }

  @Test
  void everyVisitCarriesItsOwnPartOfTheConnectedStory() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    String[] situations = {
      "spill", "laundry", "ATM", "wash", "haircut", "flower", "thank", "snack", "laundry", "lamp",
      "game", "parcel"
    };
    for (String situation : situations) {
      var current = story.current();
      assertThat(service.context(8L, current.npcCode()).orElseThrow().instruction())
          .containsIgnoringCase(situation);
      assertThat(service.context(8L, current.npcCode()).orElseThrow().state().intents())
          .allSatisfy(intent -> assertThat(intent.desc()).containsIgnoringCase("learner"));
      story.accept(story.getStage(), current.npcCode(), current.goalIds());
    }
  }

  @Test
  void progressResponseExplainsTheCurrentVisitAndShowsCompletedGoals() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", java.util.List.of("order_breakfast"));
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var response = service.status(8L);
    assertThat(response.situation()).contains("음료");
    assertThat(response.steps().getFirst().goals().getFirst().completed()).isTrue();
    assertThat(response.steps().getFirst().goals().get(1).completed()).isFalse();
  }

  @Test
  void eachVisitShowsItsOwnReadableSituationAndGoal() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    String[] expectedPlaces = {
      "식당", "빨래방", "은행", "빨래방", "이발소", "꽃집", "이발소", "빵집", "빨래방", "생활용품점", "오락실", "우체국"
    };
    for (String place : expectedPlaces) {
      var response = service.status(8L);
      var step = response.steps().get(story.getStage());
      assertThat(step.title()).contains(place);
      assertThat(step.goals().getFirst().title()).doesNotContain("_");
      assertThat(response.situation()).doesNotContain("다음 장소");
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
  }

  @Test
  void newStoryDoesNotResetTheFirstDayTravelBudget() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.getBalance()).thenReturn(12);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    assertThat(service.status(8L).balance()).isEqualTo(12);
  }

  @Test
  void finishedQuestRewardsAddToTheBudget() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.isCompleted()).thenReturn(true);
    when(completed.getBalance()).thenReturn(12);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    TownStory story = TownStory.start(8L);
    while (story.getStage() < 4) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    // 첫 퀘스트 보상 10코인과 마을 이야기 첫 장(4단계까지) 보상 6코인을 더한다.
    assertThat(service.status(8L).balance()).isEqualTo(12 + 10 + 6);
  }

  @Test
  void travelerInsideTheHotelIsFirstGuidedToTheTown() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.getScene()).thenReturn("HOTEL");
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    var response = service.status(8L);
    assertThat(response.scene()).isEqualTo("HOTEL");
    assertThat(response.situation()).contains("마을");
    assertThat(response.stage()).isZero();
  }
}
