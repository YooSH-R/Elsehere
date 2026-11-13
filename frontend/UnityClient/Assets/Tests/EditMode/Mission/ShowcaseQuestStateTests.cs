using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class ShowcaseQuestStateTests
    {
        [Test]
        public void DemoKeepsVisitorsOutOfTheBurgerShopFromStartUntilItEnds()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = 0,
            };
            Assert.That(FirstDayMissionState.ShowcaseRestaurant, Is.EqualTo("DINER"));
            Assert.That(state.ClosesShowcaseRestaurantToVisitors, Is.True);
            state.Stage = 3;
            Assert.That(state.ClosesShowcaseRestaurantToVisitors, Is.True);
            state.Completed = true;
            Assert.That(state.ClosesShowcaseRestaurantToVisitors, Is.False);
            Assert.That(
                new FirstDayMissionState
                {
                    Quest = "SHOWCASE_RAINY_EVENING",
                }.ClosesShowcaseRestaurantToVisitors,
                Is.False
            );
            Assert.That(
                new FirstDayMissionState { Started = true }.ClosesShowcaseRestaurantToVisitors,
                Is.False
            );
        }

        [Test]
        public void BorrowedUmbrellaOpensAfterHotelOnlyOnRainyStreet()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = 1,
                Raining = true,
            };
            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.True);
            Assert.That(state.OpensUmbrellaIn("HOTEL"), Is.False);
            Assert.That(state.OpensUmbrellaIn("TOWN", insideShop: true), Is.False);
            state.Weather = "CLEAR";
            state.Raining = false;
            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.False);
        }

        [Test]
        public void DemoCarriesFoldedUmbrellaFromHotelUntilReturningItButNotInsideShops()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = 1,
            };
            Assert.That(state.CarriesFoldedUmbrellaIn("HOTEL"), Is.True);
            Assert.That(state.CarriesFoldedUmbrellaIn("TOWN"), Is.True);
            Assert.That(state.CarriesFoldedUmbrellaIn("TOWN", insideShop: true), Is.False);
            state.Stage = 3;
            Assert.That(state.CarriesFoldedUmbrellaIn("TOWN"), Is.True);
            state.Completed = true;
            Assert.That(state.CarriesFoldedUmbrellaIn("HOTEL"), Is.False);
        }

        // 호텔 안에서는 빌린 우산을 접어 들고, 비 오는 거리에 나가면 펴 쓴다. 비가 그친 뒤에는 다시 접어 든다.
        [Test]
        public void DemoOpensTheBorrowedUmbrellaOnTheRainyStreetAndFoldsItIndoors()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = 1,
                Raining = true,
                Weather = "RAIN",
            };
            Assert.That(state.CarriesFoldedUmbrellaIn("HOTEL"), Is.True);
            Assert.That(state.CarriesFoldedUmbrellaIn("TOWN"), Is.False);
            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.True);
            state.Stage = 3;
            state.Raining = false;
            state.Weather = "CLEAR";
            Assert.That(state.CarriesFoldedUmbrellaIn("TOWN"), Is.True);
            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.False);
        }

        // 시연을 하는 동안 손님을 태운 택시는 신호에 걸리지 않는다. 시연이 아니면 원래대로 신호를 지킨다.
        [Test]
        public void DemoTaxiRideHoldsTheGreenOnlyWhileTheDemoRuns()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = 1,
            };
            Assert.That(state.HoldsGreenForTaxiRide, Is.True);
            state.Completed = true;
            Assert.That(state.HoldsGreenForTaxiRide, Is.False);
            Assert.That(
                new FirstDayMissionState
                {
                    Quest = "CHECK_IN",
                    Started = true,
                }.HoldsGreenForTaxiRide,
                Is.False
            );
        }

        [Test]
        public void CompletedDemoStaysAvailableForNotesWithoutStartingAnotherQuest()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Completed = true,
                Stage = 4,
            };
            Assert.That(state.ShouldOfferTownStory, Is.False);
            Assert.That(state.Destination, Is.Null);
        }

        [Test]
        public void BurgerOrderPointsToRestaurantAndDoesNotRequireCafeRest()
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = 2,
                Steps =
                {
                    new FirstDayMissionStep
                    {
                        Scene = "TOWN",
                        NpcCode = "DINER_STAFF",
                        Status = "CURRENT",
                    },
                },
            };
            Assert.That(state.Destination, Is.EqualTo("DINER_STAFF"));
            Assert.That(state.CountsAsCafeRest, Is.False);
        }
    }
}
