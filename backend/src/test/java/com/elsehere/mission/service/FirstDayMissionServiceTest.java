package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;

class FirstDayMissionServiceTest {
  private static final Map<String, String> NAMES =
      Map.of(
          "HOTEL_STAFF", "Jake", "CAFE_STAFF", "Mia", "TOWN_PASSERBY", "Ben", "SHOP_STAFF", "Alex");

  private final FirstDayMissionRepository repository = mock(FirstDayMissionRepository.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final FirstDayMissionService service =
      new FirstDayMissionService(
          repository, mock(ConversationRepository.class), mock(UserRepository.class), npcs);

  @BeforeEach
  void nameTheMissionNpcs() {
    NAMES.forEach(
        (code, name) -> {
          Npc npc = mock(Npc.class);
          when(npc.getName()).thenReturn(name);
          when(npcs.findByCode(code)).thenReturn(Optional.of(npc));
        });
  }

  // 지금 단계 담당이 아닌 NPC에게 물으면 모른다고 하지 않고 지금 만날 NPC와 장소를 알려 준다.
  @Test
  void otherNpcsSendTheLearnerToBenOnTheMainStreet() {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(FirstDayStage.RAINY_STREET)));

    FirstDayMissionService.Context context = service.context(8L, "CAFE_STAFF").orElseThrow();

    assertThat(context.instruction())
        .contains("the main street", "talk to Ben", "instead of saying you do not know");
  }

  // 다른 장소에 있으면 퀘스트 카드가 갈 곳과 만날 사람을 함께 알린다.
  @Test
  void awayFromTheStepPlaceTheCardSaysWhereAndWhoToMeet() {
    FirstDayMission mission = missionAt(FirstDayStage.RAINY_STREET);
    mission.visit("CAFE");
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    assertThat(service.status(8L).direction()).isEqualTo("마을 거리로 이동하세요.");
    assertThat(service.status(8L).situation()).isEqualTo("마을 거리에서 행인 Ben에게 물어보세요.");
  }

  @Test
  void statusKeepsStoredObjectivesAndRejectsRestUntilCafeOrderAndLocationAreReady() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", List.of("confirm_reservation"));
    when(repository.findById(8L)).thenReturn(Optional.of(mission));
    when(repository.findForUpdate(8L)).thenReturn(Optional.of(mission));
    assertThat(service.status(8L).started()).isTrue();
    assertThat(service.status(8L).steps().getFirst().goals().getFirst().completed()).isTrue();
    assertThatThrownBy(() -> service.rest(8L)).isInstanceOf(BusinessException.class);
  }

  @Test
  void statusBeforeStartPointsToQuestCard() {
    when(repository.findById(8L)).thenReturn(Optional.empty());

    assertThat(service.status(8L).direction()).isEqualTo("퀘스트 카드를 눌러 시작하세요.");
    assertThat(service.status(8L).situation()).isEqualTo("마을에서 일어난 작은 소동을 해결해 볼까요?");
  }

  // 퀘스트가 여러 개가 되므로 안내 문구에 '첫날'·'여정'을 쓰지 않는다.
  @Test
  void stageTextsDoNotTalkAboutTheFirstDayJourney() {
    for (FirstDayStage stage : FirstDayStage.values()) {
      assertThat(stage.situation()).doesNotContain("첫날").doesNotContain("여정");
      assertThat(stage.direction()).doesNotContain("첫날").doesNotContain("여정");
    }
  }

  @Test
  void staffElsewhereDirectsUmbrellaBuyersToTheDrugstoreSign() {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(FirstDayStage.BUY_UMBRELLA)));

    FirstDayMissionService.Context context = service.context(8L, "HOTEL_STAFF").orElseThrow();

    assertThat(context.instruction())
        .contains("the DRUGSTORE")
        .doesNotContainIgnoringCase("convenience");
  }

  @Test
  void hotelStaffDuringCafeBreakGetsDirectionsWithoutCafeInstructions() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    FirstDayMissionService.Context context = service.context(8L, "HOTEL_STAFF").orElseThrow();

    assertThat(context.stage()).isEqualTo(1);
    assertThat(context.state()).isNull();
    assertThat(context.instruction())
        .doesNotContain("Offer drinks")
        .doesNotContain(FirstDayStage.CAFE_BREAK.instruction())
        .contains("Corner Cafe");
  }

  @Test
  void cafeReplyAfterLastAnswerDoesNotAskAnotherMissionQuestion() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    mission.accept(1, "CAFE_STAFF", List.of("order_drink", "set_options"));
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    String instruction =
        service
            .context(8L, "CAFE_STAFF")
            .orElseThrow()
            .replyInstruction(new JudgeResult.Intent("dine_in", List.of("dine_in"), null));

    assertThat(instruction)
        .doesNotContain("ask about one remaining objective")
        .contains("No objectives remain for this stage");
  }

  @Test
  void cafeStaffAfterCompletedOrderDoesNotOfferAnotherOrderBeforeRest() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", FirstDayStage.EARLY_ARRIVAL.intentIds());
    mission.accept(1, "CAFE_STAFF", FirstDayStage.CAFE_BREAK.intentIds());
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    FirstDayMissionService.Context context = service.context(8L, "CAFE_STAFF").orElseThrow();

    assertThat(context.stage()).isEqualTo(1);
    assertThat(context.instruction()).doesNotContain("Offer drinks").contains("already ordered");
  }

  @ParameterizedTest
  @EnumSource(FirstDayStage.class)
  void everyStageKeepsOtherNpcsInTheirOwnRoles(FirstDayStage stage) {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(stage)));
    for (String npc :
        List.of(
            "HOTEL_STAFF",
            "CAFE_STAFF",
            "TOWN_PASSERBY",
            "SHOP_STAFF",
            "TAXI_DRIVER",
            "POLICE_OFFICER",
            "POST_OFFICE_STAFF",
            "DINER_STAFF",
            "BANK_STAFF",
            "LAUNDROMAT_STAFF",
            "FLOWER_SHOP_STAFF",
            "BARBER_STAFF",
            "BAKERY_STAFF",
            "ARCADE_STAFF",
            "HOME_STORE_STAFF",
            "CAFE_BARISTA",
            "HOTEL_RECEPTIONIST",
            "HOTEL_NIGHT_MANAGER",
            "SHOP_PHARMACIST",
            "SHOP_NIGHT_CLERK",
            "TOWN_RETIREE",
            "TOWN_STUDENT",
            "TOWN_EVENING_WALKER",
            "CAFE_SERVER",
            "HOTEL_CONCIERGE",
            "HOTEL_BELLHOP",
            "TOWN_SHOPPER",
            "TOWN_HANDYMAN",
            "POLICE_SERGEANT",
            "POLICE_PATROL_OFFICER",
            "POLICE_ROOKIE",
            "FIRE_CAPTAIN",
            "FIRE_FIGHTER",
            "FIRE_ROOKIE",
            "TOWN_GRANDFATHER",
            "TOWN_GRANDMOTHER")) {
      if (npc.equals(stage.npc())) {
        continue;
      }
      FirstDayMissionService.Context context = service.context(8L, npc).orElseThrow();
      assertThat(context.state()).as("%s talking to %s", stage, npc).isNull();
      assertThat(context.instruction())
          .doesNotContain(stage.instruction())
          .contains("Stay in your own role", "help with directions if asked")
          .contains("talk to " + NAMES.get(stage.npc()));
      assertThat(context.replyInstruction(new JudgeResult.Intent(null, stage.intentIds(), null)))
          .isEqualTo(context.instruction());
    }
  }

  @ParameterizedTest
  @EnumSource(FirstDayStage.class)
  void everyStageRetainsItsOwnerInstructionsAndAllowedIntents(FirstDayStage stage) {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(stage)));
    FirstDayMissionService.Context context = service.context(8L, stage.npc()).orElseThrow();

    assertThat(context.instruction()).contains(stage.instruction());
    assertThat(context.state().intents())
        .extracting("id")
        .containsExactlyElementsOf(stage.intentIds());
  }

  // 퀘스트 목표는 학습자가 먼저 말해야 영어로 말할 거리가 생긴다. 담당 NPC는 남은 목표를 먼저 꺼내거나 권하지 않고,
  // 학습자가 요청한 뒤 그 요청을 마치는 데 필요한 질문만 한다.
  @ParameterizedTest
  @EnumSource(FirstDayStage.class)
  void ownerLetsTheLearnerBringUpEveryObjective(FirstDayStage stage) {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(stage)));
    FirstDayMissionService.Context context = service.context(8L, stage.npc()).orElseThrow();

    String reply = context.replyInstruction(new JudgeResult.Intent(null, List.of(), null));

    assertThat(context.instruction()).contains("Never be the first to bring up");
    assertThat(reply)
        .contains("Never be the first to bring up")
        .doesNotContain("Ask about one of these objectives");
  }

  @ParameterizedTest
  @EnumSource(FirstDayStage.class)
  void finalAnswerInEveryStageDoesNotRepeatMissionQuestions(FirstDayStage stage) {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(stage)));
    String instruction =
        service
            .context(8L, stage.npc())
            .orElseThrow()
            .replyInstruction(new JudgeResult.Intent(null, stage.intentIds(), null));

    assertThat(instruction)
        .contains("No objectives remain for this stage")
        .doesNotContain("Ask about one of these objectives");
  }

  @Test
  void pendingQuestionExcludesStoredAndJustRecognizedGoals() {
    FirstDayMission mission = missionAt(FirstDayStage.EARLY_ARRIVAL);
    mission.accept(0, "HOTEL_STAFF", List.of("confirm_reservation"));
    when(repository.findById(8L)).thenReturn(Optional.of(mission));
    String instruction =
        service
            .context(8L, "HOTEL_STAFF")
            .orElseThrow()
            .replyInstruction(
                new JudgeResult.Intent(null, List.of("store_suitcase", "unknown"), null));

    assertThat(instruction)
        .contains("Remaining objectives after this turn: ask_cafe.")
        .doesNotContain("This turn recognized: store_suitcase,unknown");
  }

  @Test
  void missingOrFailedIntentDoesNotPretendObjectivesAreComplete() {
    when(repository.findById(8L)).thenReturn(Optional.of(missionAt(FirstDayStage.CAFE_BREAK)));
    FirstDayMissionService.Context context = service.context(8L, "CAFE_STAFF").orElseThrow();

    assertThat(context.replyInstruction(null)).isEqualTo(context.instruction());
    assertThat(context.replyInstruction(new JudgeResult.Intent(null, List.of(), "timeout")))
        .isEqualTo(context.instruction());
    assertThat(context.replyInstruction(new JudgeResult.Intent(null, List.of("unknown"), null)))
        .contains("Remaining objectives after this turn: order_drink,set_options,dine_in.")
        .doesNotContain("No objectives remain");
  }

  @Test
  void unstartedAndCompletedJourneysDoNotRestartStageInstructions() {
    when(repository.findById(8L)).thenReturn(Optional.empty());
    assertThat(service.context(8L, "HOTEL_STAFF")).isEmpty();

    FirstDayMission mission = missionAt(FirstDayStage.CHECK_IN);
    mission.accept(6, "HOTEL_STAFF", FirstDayStage.CHECK_IN.intentIds());
    when(repository.findById(8L)).thenReturn(Optional.of(mission));
    FirstDayMissionService.Context context = service.context(8L, "HOTEL_STAFF").orElseThrow();
    assertThat(context.state()).isNull();
    assertThat(context.instruction()).contains("journey is complete", "Do not restart");
  }

  private static FirstDayMission missionAt(FirstDayStage target) {
    FirstDayMission mission = FirstDayMission.start(8L);
    while (mission.current() != target) {
      FirstDayStage stage = mission.current();
      mission.accept(stage.ordinal(), stage.npc(), stage.intentIds());
      if (stage == FirstDayStage.CAFE_BREAK) {
        mission.restAtCafe();
      }
    }
    return mission;
  }

  @Test
  void statusAwayFromDrugstoreNamesItsEnglishSign() {
    FirstDayMission mission = FirstDayMission.start(8L);
    for (int stage = 0; stage < 5; stage++) {
      FirstDayStage current = mission.current();
      mission.accept(stage, current.npc(), current.intentIds());
      if (stage == 1) {
        mission.restAtCafe();
      }
    }
    mission.visit("TOWN");
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    assertThat(service.status(8L).direction()).isEqualTo("DRUGSTORE 드럭스토어로 이동하세요.");
  }

  @Test
  void weatherBeforeStartIsClear() {
    when(repository.findById(8L)).thenReturn(Optional.empty());

    assertThat(service.status(8L).weather()).isEqualTo("CLEAR");
  }

  @Test
  void weatherFollowsStageWhereverTheLearnerIs() {
    List<String> expected = List.of("CLEAR", "CLEAR", "CLEAR", "CLOUDY", "RAIN", "RAIN", "RAIN");
    for (int stage = 0; stage < expected.size(); stage++) {
      FirstDayMission mission = missionAt(stage);
      mission.visit("HOTEL");
      when(repository.findById(8L)).thenReturn(Optional.of(mission));

      assertThat(service.status(8L).weather()).as("stage %d", stage).isEqualTo(expected.get(stage));
    }
  }

  @Test
  void rainingStillNeedsTownWhileWeatherIsRain() {
    FirstDayMission mission = missionAt(4);
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    mission.visit("SHOP");
    assertThat(service.status(8L).raining()).isFalse();
    assertThat(service.status(8L).weather()).isEqualTo("RAIN");

    mission.visit("TOWN");
    assertThat(service.status(8L).raining()).isTrue();
    assertThat(service.status(8L).weather()).isEqualTo("RAIN");
  }

  @Test
  void weatherClearsWhenJourneyIsComplete() {
    FirstDayMission mission = missionAt(FirstDayStage.values().length);
    mission.visit("TOWN");
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    assertThat(mission.isCompleted()).isTrue();
    assertThat(service.status(8L).weather()).isEqualTo("CLEAR");
    assertThat(service.status(8L).raining()).isFalse();
  }

  @Test
  void finishingTheQuestAddsItsRewardToTheBudget() {
    FirstDayMission mission = missionAt(FirstDayStage.values().length);
    when(repository.findById(8L)).thenReturn(Optional.of(mission));

    // 100코인에서 카페 음료 4코인과 우산 8코인을 내고, 퀘스트 보상 10코인을 받았다.
    assertThat(service.status(8L).balance()).isEqualTo(100 - 4 - 8 + 10);
    assertThat(service.status(8L).lastPurchase()).isEqualTo("우산");
  }

  private static FirstDayMission missionAt(int target) {
    FirstDayMission mission = FirstDayMission.start(8L);
    while (mission.getStage() < target) {
      FirstDayStage current = mission.current();
      mission.accept(mission.getStage(), current.npc(), current.intentIds());
      if (mission.getStage() == 1) {
        mission.restAtCafe();
      }
    }
    return mission;
  }
}
