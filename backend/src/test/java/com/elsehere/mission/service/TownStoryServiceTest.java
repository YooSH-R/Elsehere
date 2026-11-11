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
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class TownStoryServiceTest {
  @Test
  void bankAcceptsIndirectLocationRequestsAndAsksOnlyAboutMissingWithdrawalDetails() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast", "explain_spill"));
    story.accept(1, "LAUNDROMAT_STAFF", List.of("ask_laundry", "ask_cost"));
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var context = service.context(8L, "BANK_STAFF").orElseThrow();
    assertThat(context.goals().orElseThrow().state().intents())
        .anySatisfy(
            goal -> {
              assertThat(goal.id()).isEqualTo("ask_atm");
              assertThat(goal.desc()).contains("I'm looking for an ATM");
            });

    String reply =
        context.replyInstruction(new JudgeResult.Intent("ask_atm", List.of("ask_atm"), null));
    assertThat(reply)
        .contains("Remaining objectives after this turn: ask_withdrawal.")
        .contains("says how much they want to take out")
        .contains("ask one short question about only the missing required detail")
        .doesNotContain("I'm looking for an ATM");
  }

  @Test
  void bakeryCardMakesQuantityAndRecipientAlternatives() {
    TownStory story = TownStory.start(8L);
    while (story.current() != TownStoryStage.BAKERY) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    story.resume();
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var goal = service.status(8L).steps().getFirst().goals().getFirst();

    assertThat(goal.title()).isEqualTo("간식 고르기");
    assertThat(goal.hint()).isEqualTo("간식 이름과 함께 개수나 받을 사람 중 하나 말하기");
  }

  @Test
  void everyTownVisitNamesTheNpcBothInTownAndAway() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    FirstDayMission journey = mock(FirstDayMission.class);
    when(firstDay.findById(8L)).thenReturn(Optional.of(journey));
    String[] names = {
      "Rosa", "Gus", "Henry", "Gus", "Tony", "Lily", "Tony", "Grace", "Gus", "Nina", "Max", "Linda"
    };
    for (String name : names) {
      story.resume();
      Npc owner = mock(Npc.class);
      when(owner.getName()).thenReturn(name);
      when(owner.getRoleLabel()).thenReturn("직원");
      when(npcs.findByCode(story.current().npcCode())).thenReturn(Optional.of(owner));
      for (String scene : List.of("TOWN", "HOME")) {
        when(journey.getScene()).thenReturn(scene);
        assertThat(service.status(8L).direction())
            .as(story.current() + " from " + scene)
            .contains(name);
      }
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
  }

  @Test
  void flowerIntentAcceptsAGiftWithoutRequiringAFlowerVarietyOrHintWording() {
    TownStory story = TownStory.start(8L);
    for (int index = 0; index < TownStoryStage.FLOWERS.ordinal(); index++) {
      TownStoryStage stage = story.current();
      story.accept(story.getStage(), stage.npcCode(), stage.goalIds());
      story.resume();
    }
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    var context = service.context(8L, "FLOWER_SHOP_STAFF").orElseThrow();
    var flower =
        context.goals().orElseThrow().state().intents().stream()
            .filter(intent -> intent.id().equals("choose_flower"))
            .findFirst()
            .orElseThrow();
    assertThat(flower.desc())
        .contains("flower variety is optional", "recipient OR a reason", "paraphrases", "pronouns")
        .doesNotContain("specific available flower");
    assertThat(context.goals().orElseThrow().state().accumulateHistory()).isTrue();
    assertThat(context.goals().orElseThrow().state().desc())
        .contains("Never credit NPC lines, quoted examples, negatives or hypotheticals");
    assertThat(TownStoryStage.goalHint("choose_flower")).doesNotContain("roses", "어떤 꽃");
    story.accept(story.getStage(), "FLOWER_SHOP_STAFF", List.of("choose_flower"));
    assertThat(story.current()).isEqualTo(TownStoryStage.FLOWERS);
    assertThat(story.hasGoal(0)).isTrue();
    assertThat(story.hasGoal(1)).isFalse();
  }

  private final FirstDayMissionRepository firstDay = mock(FirstDayMissionRepository.class);
  private final TownStoryRepository stories = mock(TownStoryRepository.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final TownStoryService service =
      new TownStoryService(
          stories,
          firstDay,
          npcs,
          mock(com.elsehere.conversation.repository.ConversationRepository.class));

  @Test
  void washObjectiveUsesTheSelfServiceMachineWithoutAPickupTime() {
    TownStory story = TownStory.start(8L);
    for (int index = 0; index < TownStoryStage.WASH.ordinal(); index++) {
      story.resume();
      story.accept(index, story.current().npcCode(), story.current().goalIds());
    }
    story.resume();
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var context = service.context(8L, "LAUNDROMAT_STAFF").orElseThrow();

    assertThat(context.instruction()).containsIgnoringCase("self-service");
    assertThat(context.instruction()).contains("Do not take the learner's clothes");
    assertThat(context.goals().orElseThrow().state().intents().getFirst().desc())
        .contains("Both, please.")
        .contains("No pickup or return time is required");
    assertThat(TownStoryStage.WASH.title()).doesNotContain("맡기");
    assertThat(TownStoryStage.goalHint("request_wash")).contains("세탁기").doesNotContain("찾으러");
  }

  @Test
  void laundryVisitsDescribeOperatingAndEmptyingMachinesInsteadOfAStaffService() {
    for (TownStoryStage stage :
        java.util.List.of(
            TownStoryStage.LAUNDRY, TownStoryStage.WASH, TownStoryStage.THANK_LAUNDRY)) {
      assertThat(stage.instruction()).as(stage.name()).containsIgnoringCase("self-service");
    }
    assertThat(TownStoryStage.goalDescription("collect_laundry"))
        .contains("remove their own clothes")
        .doesNotContain("asks to collect their laundry");
    assertThat(TownStoryStage.goalDescription("thank_laundromat"))
        .contains("help with the machines")
        .doesNotContain("washing their clothes");
    assertThat(TownStoryStage.goalHint("collect_laundry")).contains("꺼내");
    assertThat(com.elsehere.mission.entity.Quest.MORNING_AND_HAIRCUT.description())
        .doesNotContain("맡긴");
  }

  @Test
  void everyStoryObjectiveCanUseFactsAcrossLearnerTurns() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    for (TownStoryStage stage : TownStoryStage.values()) {
      assertThat(
              service
                  .context(8L, stage.npcCode())
                  .orElseThrow()
                  .goals()
                  .orElseThrow()
                  .state()
                  .accumulateHistory())
          .as(stage.name())
          .isTrue();
      story.accept(story.getStage(), stage.npcCode(), stage.goalIds());
      story.resume();
    }
  }

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

    QuestTalk bakery = service.context(8L, "BAKERY_STAFF").orElseThrow();

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

  // 마을 퀘스트 목표마다 무엇을 말해야 하는지 카드에 보인다(예: 아침은 음식과 음료를 함께).
  @Test
  void everyTownGoalTellsTheLearnerWhatToSay() {
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    for (TownStoryStage stage : TownStoryStage.values()) {
      for (String id : stage.goalIds()) {
        assertThat(TownStoryStage.goalHint(id)).as(id).isNotBlank();
      }
    }
    var order = service.status(8L).steps().getFirst().goals().getFirst();
    assertThat(order.id()).isEqualTo("order_breakfast");
    assertThat(order.hint()).contains("음식과 음료");
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
    assertThat(story.getStage()).isZero();
    assertThat(story.hasGoal(0)).isTrue();
    assertThat(story.hasGoal(1)).isFalse();
  }

  @Test
  void onlyTheCurrentStaffReceivesTheStoryObjectives() {
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    QuestTalk diner = service.context(8L, "DINER_STAFF").orElseThrow();
    QuestTalk bank = service.context(8L, "BANK_STAFF").orElseThrow();
    assertThat(diner.goals().orElseThrow().state().intents())
        .extracting(intent -> intent.id())
        .containsExactly("order_breakfast");
    assertThat(diner.goals().orElseThrow().state().intents().getFirst().desc())
        .containsIgnoringCase("breakfast");
    assertThat(diner.goals().orElseThrow().state().desc()).containsIgnoringCase("spill");
    assertThat(diner.instruction()).containsIgnoringCase("spill");
    assertThat(bank).isInstanceOf(QuestTalk.Guide.class);
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
      if (story.isPaused()) {
        story.resume();
      }
      var current = story.current();
      assertThat(service.context(8L, current.npcCode()).orElseThrow().instruction())
          .containsIgnoringCase(situation);
      assertThat(
              service
                  .context(8L, current.npcCode())
                  .orElseThrow()
                  .goals()
                  .orElseThrow()
                  .state()
                  .intents())
          .allSatisfy(intent -> assertThat(intent.desc()).containsIgnoringCase("learner"));
      story.accept(story.getStage(), current.npcCode(), current.goalIds());
    }
  }

  // 이야기 목표는 학습자가 먼저 말해야 한다. 담당 직원은 남은 목표를 먼저 꺼내거나 권하지 않는다.
  @Test
  void staffLetTheVisitorBringUpEveryObjective() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    for (TownStoryStage stage : TownStoryStage.values()) {
      if (story.isPaused()) {
        story.resume();
      }
      QuestTalk context = service.context(8L, stage.npcCode()).orElseThrow();

      String reply =
          context.replyInstruction(new JudgeResult.Intent(null, java.util.List.of(), null));

      assertThat(context.instruction()).as(stage.name()).contains("Never be the first to bring up");
      assertThat(reply).as(stage.name()).doesNotContain("Ask about one of these objectives");
      story.accept(story.getStage(), stage.npcCode(), stage.goalIds());
    }
  }

  // 마을 퀘스트 카드는 지금 퀘스트의 단계만 보인다(옷에 쏟은 아침과 새 머리 7단계, 고마운 마음과 선물 소포 5단계).
  @Test
  void statusShowsOnlyTheStepsOfTheCurrentQuest() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var first = service.status(8L);
    assertThat(first.quest()).isEqualTo("MORNING_AND_HAIRCUT");
    assertThat(first.steps()).hasSize(7);
    assertThat(first.stage()).isZero();

    while (story.getStage() < 8) {
      if (story.isPaused()) {
        story.resume();
      }
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    var second = service.status(8L);
    assertThat(second.quest()).isEqualTo("THANKS_AND_PARCEL");
    assertThat(second.steps()).hasSize(5);
    assertThat(second.stage()).isEqualTo(1);
    assertThat(second.steps().getFirst().title()).contains("빵집");
  }

  // 퀘스트를 끝내면 다음 퀘스트를 시작하기 전까지 진행 중인 퀘스트가 없고, 직원도 이야기 지시를 받지 않는다. 다시 시작하면 이어진다.
  @Test
  void afterAQuestEndsTheStoryWaitsForTheNextQuest() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.isCompleted()).thenReturn(true);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    TownStory story = TownStory.start(8L);
    while (story.getStage() < 7) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var waiting = service.status(8L);
    assertThat(waiting.started()).isFalse();
    assertThat(waiting.quest()).isEqualTo("THANKS_AND_PARCEL");
    assertThat(service.context(8L, "BAKERY_STAFF")).isEmpty();

    service.start(8L);

    assertThat(service.status(8L).started()).isTrue();
    assertThat(service.context(8L, "BAKERY_STAFF")).isPresent();
  }

  @Test
  void progressResponseExplainsTheCurrentVisitAndShowsCompletedGoals() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", java.util.List.of("order_breakfast"));
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    var response = service.status(8L);
    assertThat(response.situation()).contains("빈자리");
    assertThat(response.steps().getFirst().goals().getFirst().completed()).isTrue();
    assertThat(response.steps().getFirst().goals()).hasSize(1);
  }

  @Test
  void eachVisitShowsItsOwnReadableSituationAndGoal() {
    TownStory story = TownStory.start(8L);
    when(stories.findById(8L)).thenReturn(Optional.of(story));
    String[] expectedPlaces = {
      "식당", "빨래방", "은행", "빨래방", "이발소", "꽃집", "이발소", "빵집", "빨래방", "생활용품점", "오락실", "우체국"
    };
    for (String place : expectedPlaces) {
      if (story.isPaused()) {
        story.resume();
      }
      var response = service.status(8L);
      var step = response.steps().get(response.stage());
      assertThat(step.title()).contains(place);
      assertThat(step.goals().getFirst().title()).doesNotContain("_");
      assertThat(response.situation()).doesNotContain("다음 장소");
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
  }

  @Test
  void newStoryDoesNotResetTheFirstDayTravelBudget() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.isCompleted()).thenReturn(true);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    when(stories.findById(8L)).thenReturn(Optional.of(TownStory.start(8L)));

    // 첫 퀘스트에서 12코인을 내고 보상 10코인을 받은 그대로다.
    assertThat(service.status(8L).balance()).isEqualTo(100 - 12 + 10);
  }

  @Test
  void finishedQuestRewardsAddToTheBudget() {
    FirstDayMission completed = mock(FirstDayMission.class);
    when(completed.isCompleted()).thenReturn(true);
    when(firstDay.findById(8L)).thenReturn(Optional.of(completed));
    TownStory story = TownStory.start(8L);
    while (story.getStage() < 4) {
      story.accept(story.getStage(), story.current().npcCode(), story.current().goalIds());
    }
    when(stories.findById(8L)).thenReturn(Optional.of(story));

    // 첫 퀘스트: −12 +10. 마을 이야기 4단계까지 아침 7·세탁 6코인을 냈다. 두 번째 퀘스트는 아직 끝나지 않았다.
    assertThat(service.status(8L).balance()).isEqualTo(100 - 12 + 10 - 7 - 6);
    assertThat(service.status(8L).lastPurchase()).isEqualTo("세탁");
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
