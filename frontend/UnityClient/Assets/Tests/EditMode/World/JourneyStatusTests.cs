using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 미션 화면이 알려 준 코인 잔액을 폰 메뉴가 읽고, 폰의 [마을 지도]가 미션 화면에 지도를 열어 달라고 알린다.
    /// </summary>
    public class JourneyStatusTests
    {
        [TearDown]
        public void Forget() => JourneyStatus.Shared.Clear();

        [Test]
        public void ShowBalance_KeepsTheBalanceAndTellsListeners()
        {
            int changes = 0;
            JourneyStatus.Shared.Changed += () => changes++;

            JourneyStatus.Shared.ShowBalance(20);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(JourneyStatus.Shared.Balance, Is.EqualTo(20));
        }

        [Test]
        public void Clear_ForgetsTheBalance()
        {
            JourneyStatus.Shared.ShowBalance(20);

            JourneyStatus.Shared.Clear();

            Assert.That(JourneyStatus.Shared.Balance, Is.Zero);
        }

        [Test]
        public void RequestMap_TellsTheMapOwner()
        {
            bool requested = false;
            JourneyStatus.Shared.MapRequested += () => requested = true;

            JourneyStatus.Shared.RequestMap();

            Assert.That(requested, Is.True);
        }
    }
}
