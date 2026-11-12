package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;

class ShowcaseJourneyTest {
  @Test
  void anotherNpcAndFutureGoalsCannotSkipTheHotel() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "DINER_STAFF", List.of("ask_burger_location", "promise_umbrella_return"));
    journey.accept(2, "HOTEL_STAFF", List.of("ask_burger_location", "promise_umbrella_return"));
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

  private static ShowcaseJourney leaveHotel() {
    ShowcaseJourney journey = ShowcaseJourney.start(8L);
    journey.accept(0, "HOTEL_STAFF", List.of("ask_burger_location"));
    assertThat(journey.getStage()).isZero();
    journey.accept(0, "HOTEL_STAFF", List.of("promise_umbrella_return"));
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
