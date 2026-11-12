using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class ShowcaseQuestStateTests
    {
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
