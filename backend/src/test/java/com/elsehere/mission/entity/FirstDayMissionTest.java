package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;

class FirstDayMissionTest {
  @Test
  void invalidOrUnrelatedResultsCannotAdvanceAndRestRequiresCompletedOrder() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", null);
    mission.accept(0, "CAFE_STAFF", List.of("confirm_reservation", "store_suitcase", "ask_cafe"));
    mission.accept(1, "HOTEL_STAFF", List.of("confirm_reservation", "store_suitcase", "ask_cafe"));
    mission.accept(0, "HOTEL_STAFF", List.of("unclear", "chat"));
    assertThat(mission.getStage()).isZero();
    assertThat(mission.restAtCafe()).isFalse();
    mission.accept(0, "HOTEL_STAFF", List.of("confirm_reservation", "store_suitcase", "ask_cafe"));
    mission.accept(1, "CAFE_STAFF", List.of("order_drink", "order_drink"));
    assertThat(mission.restAtCafe()).isFalse();
  }

  @Test
  void hotelRequestsCanAccumulateAcrossTurnsAndUnlockCafe() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", List.of("confirm_reservation", "store_suitcase"));
    assertThat(mission.getStage()).isZero();
    mission.accept(0, "HOTEL_STAFF", List.of("ask_cafe"));
    assertThat(mission.getStage()).isEqualTo(1);
  }

  @Test
  void completeStoryRestoresBagBuysOneUmbrellaAndChecksIn() {
    FirstDayMission mission = FirstDayMission.start(8L);
    mission.accept(0, "HOTEL_STAFF", List.of("confirm_reservation", "store_suitcase", "ask_cafe"));
    mission.accept(1, "CAFE_STAFF", List.of("order_drink", "set_options", "dine_in"));
    assertThat(mission.getStage()).isEqualTo(1);
    assertThat(mission.restAtCafe()).isTrue();
    assertThat(mission.getStage()).isEqualTo(2);
    mission.accept(2, "HOTEL_STAFF", List.of("report_left_bag", "return_later"));
    mission.accept(3, "CAFE_STAFF", List.of("ask_lost_bag", "describe_bag", "describe_contents"));
    assertThat(mission.getStage()).isEqualTo(3);
    mission.accept(3, "CAFE_STAFF", List.of("confirm_ownership"));
    assertThat(mission.getStage()).isEqualTo(4);
    mission.accept(4, "TOWN_PASSERBY", List.of("ask_umbrella_shop", "ask_directions"));
    mission.accept(5, "SHOP_STAFF", List.of("ask_price", "buy_umbrella", "choose_payment"));
    assertThat(mission.getStage()).isEqualTo(6);
    assertThat(mission.getBalance()).isEqualTo(12);
    mission.accept(6, "HOTEL_STAFF", List.of("found_bag", "request_checkin", "show_reservation"));
    assertThat(mission.isCompleted()).isFalse();
    mission.accept(6, "HOTEL_STAFF", List.of("collect_suitcase"));
    assertThat(mission.isCompleted()).isTrue();
    mission.accept(5, "SHOP_STAFF", List.of("buy_umbrella", "choose_payment"));
    assertThat(mission.getBalance()).isEqualTo(12);
  }
}
