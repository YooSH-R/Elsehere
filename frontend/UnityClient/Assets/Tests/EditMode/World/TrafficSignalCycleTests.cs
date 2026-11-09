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
