using NUnit.Framework;

namespace A305.World.Tests
{
    public class ClosingCountdownTests
    {
        // 1일차 21:00. 게임 시각은 1일차 0시부터 센 분이다.
        private const double ClosingTime = 21 * 60;

        private ClosingCountdown countdown;

        [SetUp]
        public void CreateCountdown()
        {
            countdown = new ClosingCountdown();
        }

        [Test]
        public void New_IsOpen()
        {
            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.Open));
        }

        [Test]
        public void Update_WhileOpen_StaysOpen()
        {
            countdown.Update(isOpen: true, ClosingTime - 10);

            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.Open));
        }

        [Test]
        public void Update_WhenThePlaceCloses_StartsCountingDownTheGracePeriod()
        {
            countdown.Update(isOpen: true, ClosingTime - 1);
            countdown.Update(isOpen: false, ClosingTime);

            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.CountingDown));
            Assert.That(countdown.MinutesLeft, Is.EqualTo(30));
            Assert.That(ClosingCountdown.GraceMinutes, Is.EqualTo(30));
        }

        [Test]
        public void Update_AsGameTimePasses_CountsDownInGameMinutes()
        {
            countdown.Update(isOpen: false, ClosingTime);
            countdown.Update(isOpen: false, ClosingTime + 10);

            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.CountingDown));
            Assert.That(countdown.MinutesLeft, Is.EqualTo(20));
        }

        [Test]
        public void Update_WithPartOfAMinuteLeft_RoundsUp()
        {
            countdown.Update(isOpen: false, ClosingTime);
            countdown.Update(isOpen: false, ClosingTime + 0.5);
            Assert.That(countdown.MinutesLeft, Is.EqualTo(30));

            countdown.Update(isOpen: false, ClosingTime + 29.5);
            Assert.That(countdown.MinutesLeft, Is.EqualTo(1));
        }

        [Test]
        public void Update_AfterTheGracePeriod_IsTimeToLeave()
        {
            countdown.Update(isOpen: false, ClosingTime);
            countdown.Update(isOpen: false, ClosingTime + 30);

            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.TimeToLeave));
            Assert.That(countdown.MinutesLeft, Is.EqualTo(0));
        }

        [Test]
        public void Update_WhenThePlaceReopens_StopsAndCountsTheFullPeriodNextTime()
        {
            countdown.Update(isOpen: false, ClosingTime);
            countdown.Update(isOpen: false, ClosingTime + 20);

            countdown.Update(isOpen: true, ClosingTime + 25);
            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.Open));

            countdown.Update(isOpen: false, ClosingTime + 60);
            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.CountingDown));
            Assert.That(countdown.MinutesLeft, Is.EqualTo(30));
        }

        [Test]
        public void Update_WhenTheClockGoesBack_DoesNotCountMoreThanTheGracePeriod()
        {
            // 서버 시각으로 맞추면 신호 사이에 흘린 시각보다 조금 뒤로 갈 수 있다.
            countdown.Update(isOpen: false, ClosingTime);
            countdown.Update(isOpen: false, ClosingTime - 2);

            Assert.That(countdown.MinutesLeft, Is.EqualTo(30));
        }

        [Test]
        public void Reset_ForgetsTheCountdown()
        {
            // 다른 장소로 옮기면 그 장소의 영업이 끝난 때부터 다시 센다.
            countdown.Update(isOpen: false, ClosingTime);
            countdown.Update(isOpen: false, ClosingTime + 20);

            countdown.Reset();
            Assert.That(countdown.Phase, Is.EqualTo(ClosingPhase.Open));

            countdown.Update(isOpen: false, ClosingTime + 25);
            Assert.That(countdown.MinutesLeft, Is.EqualTo(30));
        }
    }
}
