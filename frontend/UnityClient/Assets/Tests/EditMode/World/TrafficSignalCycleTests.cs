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
    }
}
