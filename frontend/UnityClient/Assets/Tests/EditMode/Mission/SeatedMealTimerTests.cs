using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class SeatedMealTimerTests
    {
        [Test]
        public void OnlyFiveActuallySeatedSecondsCompleteTheMealOnce()
        {
            var timer = new SeatedMealTimer();
            Assert.That(timer.Tick(100, false), Is.False);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(5));
            Assert.That(timer.Tick(4, true), Is.False);
            Assert.That(timer.Tick(100, false), Is.False);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(1));
            Assert.That(timer.Tick(.9f, true), Is.False);
            Assert.That(timer.Tick(.1f, true), Is.True);
            Assert.That(timer.IsComplete, Is.True);
            Assert.That(timer.Tick(10, true), Is.False);
            timer.Reset();
            Assert.That(timer.IsComplete, Is.False);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(5));
        }
    }
}
