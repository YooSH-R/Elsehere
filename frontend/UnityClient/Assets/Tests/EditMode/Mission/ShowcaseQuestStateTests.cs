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

        // 시연은 순서대로 한다. 주문 단계 전에는 햄버거 가게 직원이 주문을 받지 않고, 호텔 단계에는 지나가는 택시를 세우지 않는다.
        // 막은 까닭은 지금 단계를 알려 준다.
        [Test]
        public void BeforeTheOrderStep_RosaAndTheHailedTaxiWaitWithTheCurrentStepAsTheReason()
        {
            var locks = Showcase(stage: 0, "호텔에서 저녁 나들이 준비").ShowcaseTalkLocks;

            Assert.That(locks.Keys, Is.EquivalentTo(new[] { "DINER_STAFF", "TAXI_DRIVER" }));
            Assert.That(
                locks["DINER_STAFF"],
                Does.Contain("시연").And.Contain("호텔에서 저녁 나들이 준비")
            );

            locks = Showcase(stage: 1, "택시로 햄버거 가게 가기").ShowcaseTalkLocks;

            Assert.That(
                locks.Keys,
                Is.EquivalentTo(new[] { "DINER_STAFF" }),
                "택시 단계에는 택시를 탄다."
            );
            Assert.That(locks["DINER_STAFF"], Does.Contain("택시로 햄버거 가게 가기"));
        }

        [Test]
        public void AtTheOrderStep_AfterFinishing_OrOutsideTheShowcase_NothingIsLocked()
        {
            Assert.That(Showcase(stage: 2, "햄버거와 사이드 주문").ShowcaseTalkLocks, Is.Empty);
            FirstDayMissionState finished = Showcase(stage: 3, null);
            finished.Completed = true;
            Assert.That(finished.ShowcaseTalkLocks, Is.Empty);
            Assert.That(
                new FirstDayMissionState { Quest = "CHECK_IN", Started = true }.ShowcaseTalkLocks,
                Is.Empty
            );
            Assert.That(
                new FirstDayMissionState { Quest = "SHOWCASE_RAINY_EVENING" }.ShowcaseTalkLocks,
                Is.Empty,
                "시작하지 않은 시연은 아무것도 막지 않는다."
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
