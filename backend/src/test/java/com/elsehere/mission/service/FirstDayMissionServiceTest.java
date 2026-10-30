package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.user.repository.UserRepository;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.Test;

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
  void statusAwayFromConvenienceStoreNamesItsEnglishSign() {
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

    assertThat(service.status(8L).direction()).isEqualTo("CONVENIENCE STORE 편의점으로 이동하세요.");
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
