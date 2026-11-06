package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class PoliceOfficersTest {

  @Test
  void taylorRemainsAnOfficerAndKevinWorksAtTheBakery() {
    assertThat(PoliceOfficers.includes("POLICE_ROOKIE")).isTrue();
    assertThat(PoliceOfficers.includes("POLICE_PATROL_OFFICER")).isFalse();
    assertThat(PoliceOfficers.includes("POLICE_OFFICER")).isTrue();
    assertThat(PoliceOfficers.includes("POLICE_SERGEANT")).isTrue();
  }
}
