using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class CafeSeatTimerTests
    {
        [Test]
        public void RestCompletesOnlyAfterHoldingSeatForRequiredTime()
        {
            var timer = new CafeSeatTimer(2f);

            timer.Begin();

            Assert.That(timer.Tick(1.9f), Is.False);
            Assert.That(timer.Tick(.1f), Is.True);
            Assert.That(timer.Tick(1f), Is.False);
        }

        [Test]
        public void LeavingSeatCancelsRest()
        {
            var timer = new CafeSeatTimer(2f);

            timer.Begin();
            timer.Tick(1f);
            timer.Cancel();

            Assert.That(timer.Tick(2f), Is.False);
        }
    }
}
