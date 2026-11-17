package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;

class ShowcaseJourneyTest {
  // 시연을 처음으로 되돌리면 시작 전 상태로 둔다. 시연자가 퀘스트 창에서 눌러야 진행이 시작된다.
  @Test
  void restartGoesBackToTheBeginningWithoutStarting() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    assertThat(journey.isActive()).isTrue();
    journey.accept(0, "HOTEL_STAFF", List.of("ask_for_taxi"));

    journey.restart();

    assertThat(journey.getStage()).isZero();
    assertThat(journey.hasUmbrella()).isFalse();
    assertThat(journey.isActive()).isFalse();
    journey.resume();
    assertThat(journey.isActive()).isTrue();
  }

  @Test
  void anotherNpcAndFutureGoalsCannotSkipTheHotel() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "DINER_STAFF", List.of("ask_for_taxi"));
    journey.accept(2, "HOTEL_STAFF", List.of("ask_for_taxi"));
    journey.accept(0, "HOTEL_STAFF", List.of("order_burger", "thank_for_umbrella"));
    assertThat(journey.getStage()).isZero();
    assertThat(journey.hasUmbrella()).isFalse();
  }

  @Test
  void taxiRequiresARecognizedBurgerDestinationAndActualArrival() {
    ShowcaseJourney journey = leaveHotel();
    assertThat(journey.current()).isEqualTo(ShowcaseStage.TAXI);
    journey.arriveByTaxi("DINER");
    journey.visit("TOWN");
    assertThat(journey.current()).isEqualTo(ShowcaseStage.TAXI);
    journey.chooseTaxiDestination("TAXI_DRIVER", "HOTEL");
    journey.arriveByTaxi("DINER");
    assertThat(journey.current()).isEqualTo(ShowcaseStage.TAXI);
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    assertThat(journey.current()).isEqualTo(ShowcaseStage.TAXI);
    journey.arriveByTaxi("DINER");
    assertThat(journey.current()).isEqualTo(ShowcaseStage.ORDER);
  }

  @Test
  void orderChargesOnceAndClearsTheRainWithoutChangingAnotherJourney() {
    ShowcaseJourney journey = atBurgerShop();
    journey.accept(2, "DINER_STAFF", List.of("order_burger", "customize_burger"));
    assertThat(journey.balance()).isEqualTo(100);
    assertThat(journey.weather()).isEqualTo("RAIN");
    journey.accept(2, "DINER_STAFF", List.of("add_sides_drink", "choose_dining"));
    assertThat(journey.balance()).isEqualTo(93);
    assertThat(journey.weather()).isEqualTo("CLEAR");
    assertThat(journey.current()).isEqualTo(ShowcaseStage.RETURN);
    journey.accept(2, "DINER_STAFF", List.of("order_burger", "choose_dining"));
    journey.accept(3, "HOTEL_STAFF", List.of("thank_for_umbrella"));
    assertThat(journey.isCompleted()).isTrue();
    assertThat(journey.hasUmbrella()).isFalse();
    assertThat(journey.balance()).isEqualTo(93);
    assertThat(FirstDayMission.start(8L).getStage()).isZero();
  }

  // 시연 대본에서는 우산 반환 약속을 하지 않는다. 택시만 부탁하면 호텔 단계를 마치고, 직원이 빌려준 우산을 든다.
  @Test
  void askingForATaxiAloneFinishesTheHotelWithTheLentUmbrella() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);

    journey.accept(0, "HOTEL_STAFF", List.of("ask_for_taxi"));

    assertThat(journey.current()).isEqualTo(ShowcaseStage.TAXI);
    assertThat(journey.hasUmbrella()).isTrue();
  }

  private static ShowcaseJourney leaveHotel() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", List.of("ask_for_taxi"));
    assertThat(journey.hasUmbrella()).isTrue();
    return journey;
  }

  private static ShowcaseJourney atBurgerShop() {
    ShowcaseJourney journey = leaveHotel();
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    return journey;
  }
}
