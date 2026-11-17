using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 교차로 신호는 본길 초록 → 노랑 → 빨강을 돌고, 본길이 빨강인 동안 가로길이 초록 → 노랑이 된다.
    /// </summary>
    public class TrafficSignalCycleTests
    {
        private const float MainGreen = 12f;
        private const float CrossGreen = 7f;
        private const float Yellow = 3f;

        private TrafficSignalCycle cycle;

        [SetUp]
        public void CreateCycle()
        {
            cycle = new TrafficSignalCycle(MainGreen, CrossGreen, Yellow);
        }

        // 본길 초록을 붙잡을 때는 다음 주기의 본길 초록 시작으로 넘어간다. 주기는 12 + 3 + 7 + 3 = 25초다.
        [TestCase(20f, 25f)]
        [TestCase(30f, 50f)]
        public void NextMainGreen_IsTheStartOfTheNextMainGreen(float now, float expected)
        {
            Assert.That(cycle.NextMainGreen(now), Is.EqualTo(expected).Within(.001f));
            Assert.That(cycle.MainAt(cycle.NextMainGreen(now)), Is.EqualTo(SignalLight.Green));
        }

        // 손님을 태운 택시가 지나는 동안 본길 신호를 초록으로 붙잡는다. 풀면 원래 주기대로 다시 흐른다.
        [Test]
        public void TrafficLights_HoldingTheMainGreen_KeepsItGreenUntilReleased()
        {
            var lights = new UnityEngine.GameObject("Lights").AddComponent<TrafficLights>();
            try
            {
                lights.Advance(20f);
                Assert.That(lights.Main, Is.Not.EqualTo(SignalLight.Green), "본길 빨간불");

                lights.HoldsMainGreen = true;
                lights.Advance(1f);
                Assert.That(lights.Main, Is.EqualTo(SignalLight.Green), "붙잡으면 바로 초록");
                lights.Advance(100f);
                Assert.That(lights.Main, Is.EqualTo(SignalLight.Green), "붙잡는 동안 계속 초록");

                lights.HoldsMainGreen = false;
                lights.Advance(20f);
                Assert.That(lights.Main, Is.Not.EqualTo(SignalLight.Green), "풀면 다시 주기대로");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lights.gameObject);
            }
        }

        [Test]
        public void SecondsToCross_IsTheWalkAtPedestrianPacePlusAMargin()
        {
            // 6.4m를 1.6m/s로 4초에 건너고 1초 여유를 더한다.
            Assert.That(TrafficSignalCycle.SecondsToCross(6.4f), Is.EqualTo(5f).Within(.001f));
        }

        // 본길 빨간불은 15~25초다. 5초면 건너는 횡단보도는 20초까지 걷기 신호다(8초 고정이면 17초까지뿐이었다).
        [TestCase(15f, PedestrianLight.Walk)]
        [TestCase(19f, PedestrianLight.Walk)]
        [TestCase(20f, PedestrianLight.Walk)]
        [TestCase(20.01f, PedestrianLight.Clearance)]
        [TestCase(14.99f, PedestrianLight.Stop)]
        public void PedestrianSignal_StaysWalkWhileTheCrosswalkCanStillBeCrossed(
            float seconds,
            PedestrianLight expected
        )
        {
            Assert.That(cycle.PedestrianAt(seconds, true, 5f), Is.EqualTo(expected));
        }

        [Test]
        public void MainStreet_StartsGreenThenTurnsYellowThenRed()
        {
            Assert.That(cycle.MainAt(0f), Is.EqualTo(SignalLight.Green));
            Assert.That(cycle.MainAt(MainGreen - .1f), Is.EqualTo(SignalLight.Green));
            Assert.That(cycle.MainAt(MainGreen + .5f), Is.EqualTo(SignalLight.Yellow));
            Assert.That(cycle.MainAt(MainGreen + Yellow + .5f), Is.EqualTo(SignalLight.Red));
        }

        [Test]
        public void CrossStreet_GoesOnlyWhileMainStreetIsRed()
        {
            Assert.That(cycle.CrossAt(0f), Is.EqualTo(SignalLight.Red));
            Assert.That(cycle.CrossAt(MainGreen + .5f), Is.EqualTo(SignalLight.Red));
            Assert.That(cycle.CrossAt(MainGreen + Yellow + .5f), Is.EqualTo(SignalLight.Green));
            Assert.That(
                cycle.CrossAt(MainGreen + Yellow + CrossGreen + .5f),
                Is.EqualTo(SignalLight.Yellow)
            );
            Assert.That(
                cycle.MainAt(MainGreen + Yellow + CrossGreen + .5f),
                Is.EqualTo(SignalLight.Red)
            );
        }

        [Test]
        public void Cycle_Repeats()
        {
            float length = MainGreen + Yellow + CrossGreen + Yellow;

            Assert.That(cycle.MainAt(length + 1f), Is.EqualTo(SignalLight.Green));
            Assert.That(cycle.CrossAt(length + 1f), Is.EqualTo(SignalLight.Red));
        }

        [TestCase(0f, true, PedestrianLight.Stop)]
        [TestCase(0f, false, PedestrianLight.Walk)]
        [TestCase(7f, false, PedestrianLight.Walk)]
        [TestCase(7.01f, false, PedestrianLight.Clearance)]
        [TestCase(14.99f, true, PedestrianLight.Stop)]
        [TestCase(15f, true, PedestrianLight.Walk)]
        [TestCase(15f, false, PedestrianLight.Stop)]
        [TestCase(17f, true, PedestrianLight.Walk)]
        [TestCase(17.01f, true, PedestrianLight.Clearance)]
        [TestCase(24.99f, true, PedestrianLight.Clearance)]
        [TestCase(25f, true, PedestrianLight.Stop)]
        [TestCase(40f, true, PedestrianLight.Walk)]
        public void PedestrianSignal_MatchesTheCrossedRoadAndLeavesTimeToFinish(
            float seconds,
            bool crossesMainRoad,
            PedestrianLight expected
        )
        {
            Assert.That(cycle.PedestrianAt(seconds, crossesMainRoad), Is.EqualTo(expected));
            if (expected != PedestrianLight.Stop)
                Assert.That(
                    crossesMainRoad ? cycle.MainAt(seconds) : cycle.CrossAt(seconds),
                    Is.EqualTo(SignalLight.Red),
                    "보행자가 건너는 동안 가로지르는 차로는 빨간불이어야 합니다."
                );
        }
    }
}
