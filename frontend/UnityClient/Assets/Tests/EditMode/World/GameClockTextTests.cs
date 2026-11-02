using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>HUD 시계 칩은 게임 시각을 "n일차 · 오전/오후 h:mm"으로 보인다.</summary>
    public class GameClockTextTests
    {
        [TestCase(480, "1일차 · 오전 8:00")]
        [TestCase(820, "1일차 · 오후 1:40")]
        [TestCase(720, "1일차 · 오후 12:00")]
        [TestCase(1439.9, "1일차 · 오후 11:59")]
        [TestCase(1445, "2일차 · 오전 12:05")]
        public void Format_ShowsTheDayAndATwelveHourTime(double minutes, string expected)
        {
            Assert.That(GameClockText.Format(minutes), Is.EqualTo(expected));
        }
    }
}
