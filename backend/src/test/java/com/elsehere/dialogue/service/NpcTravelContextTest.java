package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.dialogue.dto.NpcTravelContext;
import com.elsehere.dialogue.dto.NpcTravelContext.Phase;
import org.junit.jupiter.api.Test;

class NpcTravelContextTest {
  @Test
  void commuteFactsExplainTheDistanceAndEarlyDeparture() {
    String facts = new NpcTravelContext(Phase.GOING_TO_WORK, 80, 35, 190, 0).describe();
    assertThat(facts)
        .contains("travelling to work", "80", "35", "190", "game minutes", "tired", "leave early");
  }

  @Test
  void homeAndLeisureDoNotPretendToBeLateForWork() {
    assertThat(new NpcTravelContext(Phase.GOING_HOME, 40, 30, 60, 0).describe())
        .contains("going home")
        .doesNotContain("late for work");
    assertThat(new NpcTravelContext(Phase.LEISURE, 20, 10, 60, 0).describe())
        .contains("free time")
        .doesNotContain("late for work");
  }

  @Test
  void arrivalKeepsTheRealWalkWithoutClaimingStillTravelling() {
    assertThat(new NpcTravelContext(Phase.AT_WORK, 80, 85, 190, 0).describe())
        .contains("arrived at work", "85")
        .doesNotContain("travelling to work");
  }

  @Test
  void actualDelayIsSuppliedAsGameTime() {
    assertThat(new NpcTravelContext(Phase.GOING_TO_WORK, 80, 35, 190, 12).describe())
        .contains("12", "late for work");
  }
}
