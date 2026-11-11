using NUnit.Framework;

namespace A305.Mission.Tests
{
    // 우산은 5단계(드럭스토어)에서 산다. 산 뒤에는 비 오는 마을에서만 펴 쓴다.
    public class FirstDayUmbrellaTests
    {
        private const int BuyUmbrellaStage = 5;

        [Test]
        public void OpensUmbrella_InTheRainyTownAfterBuyingIt()
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);

            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.True);
        }

        [TestCase(BuyUmbrellaStage - 1)]
        [TestCase(BuyUmbrellaStage)]
        public void KeepsUmbrellaAway_BeforeBuyingIt(int stage)
        {
            FirstDayMissionState state = Rainy(stage);

            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.False);
        }

        // 마을 매장 실내 장면은 서버 장소가 마을(TOWN)이지만 실내다.
        [Test]
        public void KeepsUmbrellaAway_InsideATownShop()
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);

            Assert.That(state.OpensUmbrellaIn("TOWN", insideShop: true), Is.False);
        }

        [TestCase("SHOP")]
        [TestCase("HOTEL")]
        public void KeepsUmbrellaAway_Indoors(string sceneCode)
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);

            Assert.That(state.OpensUmbrellaIn(sceneCode), Is.False);
        }

        [Test]
        public void KeepsUmbrellaAway_WhenItIsNotRaining()
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);
            state.Weather = "CLEAR";
            state.Raining = false;

            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.False);
        }

        [Test]
        public void OpensUmbrella_AfterTheJourneyWhileItStillRains()
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);
            state.Completed = true;

            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.True);
        }

        [Test]
        public void OpensUmbrella_WithTheRainingFlagFromAServerWithoutForecast()
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);
            state.Weather = null;

            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.True);
        }

        [Test]
        public void KeepsUmbrellaAway_BeforeTheJourneyStarts()
        {
            FirstDayMissionState state = Rainy(stage: BuyUmbrellaStage + 1);
            state.Started = false;

            Assert.That(state.OpensUmbrellaIn("TOWN"), Is.False);
        }

        private static FirstDayMissionState Rainy(int stage) =>
            new FirstDayMissionState
            {
                Started = true,
                Stage = stage,
                Raining = true,
                Weather = "RAIN",
            };
    }
}
