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

        // 폰 잠금 화면은 큰 시각과 그 아래 날짜를 따로 보이고, 상태 표시줄에는 오전·오후와 시각을 보인다.
        [TestCase(560, "9:20", "1일차 · 오전", "오전 9:20")]
        [TestCase(1445, "12:05", "2일차 · 오전", "오전 12:05")]
        [TestCase(820, "1:40", "1일차 · 오후", "오후 1:40")]
        public void Parts_SplitTheClockForThePhone(
            double minutes,
            string time,
            string day,
            string statusBar
        )
        {
            Assert.That(GameClockText.Time(minutes), Is.EqualTo(time));
            Assert.That(GameClockText.DayAndHalf(minutes), Is.EqualTo(day));
            Assert.That(GameClockText.HalfAndTime(minutes), Is.EqualTo(statusBar));
        }
    }
}
