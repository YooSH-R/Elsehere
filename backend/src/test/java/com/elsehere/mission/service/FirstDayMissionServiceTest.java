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
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;

class FirstDayMissionServiceTest {
  private final FirstDayMissionRepository repository = mock(FirstDayMissionRepository.class);
  private final FirstDayMissionService service =
      new FirstDayMissionService(
          repository, mock(ConversationRepository.class), mock(UserRepository.class));

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
  void statusBeforeStartPointsToMapButton() {
    when(repository.findById(8L)).thenReturn(Optional.empty());

    assertThat(service.status(8L).direction()).isEqualTo("왼쪽 위 지도 버튼에서 첫날 여정을 시작하세요.");
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
            "POLICE_OFFICER")) {
      if (npc.equals(stage.npc())) {
        continue;
      }
      FirstDayMissionService.Context context = service.context(8L, npc).orElseThrow();
      assertThat(context.state()).as("%s talking to %s", stage, npc).isNull();
      assertThat(context.instruction())
          .doesNotContain(stage.instruction())
          .contains("Stay in your own role", "help with directions if asked");
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
