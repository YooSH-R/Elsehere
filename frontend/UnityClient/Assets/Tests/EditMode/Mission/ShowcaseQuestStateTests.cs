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

        // 이 퀘스트는 정해진 순서대로만 한다. 단계마다 그 단계에 나오는 사람만 말을 걸 수 있고, 나머지(다른 NPC·지나가는 택시)는
        // 지금 차례를 까닭으로 막는다. 까닭에 '시연'이라는 말은 쓰지 않는다.
        [Test]
        public void EachStepAllowsOnlyItsOwnPeopleAndTellsWhoseTurnItIs()
        {
            FirstDayMissionState hotel = Showcase(stage: 0, "호텔에서 저녁 나들이 준비");

            Assert.That(
                hotel.ShowcaseStepNpcs,
                Is.EquivalentTo(new[] { "HOTEL_STAFF", "HOTEL_RECEPTIONIST", "HOTEL_BELLHOP" })
            );
            Assert.That(
                hotel.ShowcaseOrderReason,
                Does.Contain("정해진 순서")
                    .And.Contain("호텔에서 저녁 나들이 준비")
                    .And.Not.Contain("시연")
            );
            Assert.That(
                Showcase(stage: 1, "택시로 햄버거 가게 가기").ShowcaseStepNpcs,
                Is.EquivalentTo(new[] { "TAXI_DRIVER" })
            );
            Assert.That(
                Showcase(stage: 2, "햄버거와 사이드 주문").ShowcaseStepNpcs,
                Is.EquivalentTo(new[] { "DINER_STAFF", "TOWN_JOHN_PARK" })
            );
        }

        [Test]
        public void AfterFinishing_OrOutsideTheQuest_NothingIsHeldToTheOrder()
        {
            FirstDayMissionState finished = Showcase(stage: 3, null);
            finished.Completed = true;
            Assert.That(finished.ShowcaseOrderReason, Is.Null);
            Assert.That(
                new FirstDayMissionState { Quest = "CHECK_IN", Started = true }.ShowcaseOrderReason,
                Is.Null
            );
            Assert.That(
                new FirstDayMissionState { Quest = "SHOWCASE_RAINY_EVENING" }.ShowcaseOrderReason,
                Is.Null,
                "시작하지 않았으면 아무것도 막지 않는다."
            );
        }

        private static FirstDayMissionState Showcase(int stage, string currentTitle)
        {
            var state = new FirstDayMissionState
            {
                Quest = "SHOWCASE_RAINY_EVENING",
                Started = true,
                Stage = stage,
            };
            for (int i = 0; i < 3; i++)
                state.Steps.Add(
                    new FirstDayMissionStep
                    {
                        Index = i,
                        Title = i == stage ? currentTitle : "다른 단계",
                        Status =
                            i < stage ? "DONE"
                            : i == stage ? "CURRENT"
                            : "PENDING",
                    }
                );
            return state;
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
