using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 미션 화면이 알려 준 오늘의 여정(단계 칩·할 일·진행·예산)을 폰 메뉴가 읽고, 폰의 [마을 지도]가 미션 화면에 지도를 열어 달라고 알린다.
    /// </summary>
    public class JourneyStatusTests
    {
        private static readonly JourneyMark[] Marks =
        {
            JourneyMark.Done,
            JourneyMark.Current,
            JourneyMark.Upcoming,
        };

        [TearDown]
        public void Forget() => JourneyStatus.Shared.Clear();

        [Test]
        public void Show_KeepsTheJourneyAndTellsListeners()
        {
            int changes = 0;
            JourneyStatus.Shared.Changed += () => changes++;

            JourneyStatus.Shared.Show("2 / 3", "카페 직원에게 요청하세요.", Marks, 20);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(JourneyStatus.Shared.HasJourney, Is.True);
            Assert.That(JourneyStatus.Shared.StepChip, Is.EqualTo("2 / 3"));
            Assert.That(JourneyStatus.Shared.Direction, Is.EqualTo("카페 직원에게 요청하세요."));
            Assert.That(JourneyStatus.Shared.Marks, Is.EqualTo(Marks));
            Assert.That(JourneyStatus.Shared.Balance, Is.EqualTo(20));
        }

        [Test]
        public void Clear_ForgetsTheJourney()
        {
            JourneyStatus.Shared.Show("2 / 3", "카페 직원에게 요청하세요.", Marks, 20);

            JourneyStatus.Shared.Clear();

            Assert.That(JourneyStatus.Shared.HasJourney, Is.False);
            Assert.That(JourneyStatus.Shared.Marks, Is.Empty);
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
