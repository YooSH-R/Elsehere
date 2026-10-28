package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.FirstDayMission;
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
}
