using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// NPC가 먼저 말을 걸어 부탁하는 때는 플레이어 기준 플레이 시간으로 센다(docs/npc-life-design.md 7.6). 첫 말 걸기는 접속한 뒤 플레이 2분부터,
    /// 그 뒤로는 서버에 20초마다 묻고(간격 5분은 서버가 지킴), 말을 걸고 플레이 1분 안에 대화를 시작하지 않으면 거절로 본다.
    /// </summary>
    public class FavorCallScheduleTests
    {
        [Test]
        public void TheFirstAskWaitsForTwoMinutesOfPlay()
        {
            var schedule = new FavorCallSchedule();

            schedule.Play(119);
            Assert.That(schedule.TakeAskTurn(), Is.False);

            schedule.Play(1);
            Assert.That(schedule.TakeAskTurn(), Is.True);
        }

        [Test]
        public void AfterThatItAsksEveryTwentySecondsOfPlay()
        {
            var schedule = new FavorCallSchedule();
            schedule.Play(120);
            schedule.TakeAskTurn();

            schedule.Play(19);
            Assert.That(schedule.TakeAskTurn(), Is.False);
            schedule.Play(1);
            Assert.That(schedule.TakeAskTurn(), Is.True);
        }

        [Test]
        public void PausedTimeIsNotPlay()
        {
            var schedule = new FavorCallSchedule();

            schedule.Play(-5);
            schedule.Play(0);

            Assert.That(schedule.PlaySeconds, Is.Zero);
        }

        [Test]
        public void ACallWithoutAnAnswerForAMinuteOfPlayIsOverdue()
        {
            var schedule = new FavorCallSchedule();
            schedule.Play(130);
            schedule.Called();

            schedule.Play(59);
            Assert.That(schedule.IsAnswerOverdue, Is.False);
            schedule.Play(1);
            Assert.That(schedule.IsAnswerOverdue, Is.True);

            schedule.Answered();
            Assert.That(schedule.IsWaitingForAnswer, Is.False);
            Assert.That(schedule.IsAnswerOverdue, Is.False);
        }

        [Test]
        public void FavorHint_NamesWhoWantsAFavor()
        {
            Assert.That(
                PasserbyOfferText.FavorHint("Jake"),
                Is.EqualTo("Jake이(가) 부탁할 게 있대요")
            );
            Assert.That(
                PasserbyOfferText.FavorHint("바리스타"),
                Is.EqualTo("바리스타가 부탁할 게 있대요")
            );
            Assert.That(PasserbyOfferText.FavorHint(null), Is.EqualTo("누군가 부탁할 게 있대요"));
        }
    }
}
