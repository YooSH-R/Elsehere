using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 장소 연습 방문은 [연습하기]부터 그 대화가 끝날 때까지 이어진다. 왼쪽 위 퀘스트 카드는 그동안 이야기 대신 연습을 보이고,
    /// 시작을 한 번만 알린다(장면을 옮겨도 새 장면의 카드가 이어서 알린다).
    /// </summary>
    public sealed class PracticeVisitTests
    {
        [Test]
        public void Visit_IsRememberedWithItsGoalProgressUntilItEnds()
        {
            var visit = new PracticeVisit();
            int changes = 0;
            visit.Changed += () => changes++;
            var plan = new PracticeVisitPlan
            {
                Title = "자리 잡기",
                Goals = new[]
                {
                    new PracticeVisitGoal("인원 말하고 창가 자리 부탁하기", ""),
                    new PracticeVisitGoal("기다리는 시간 묻기", ""),
                },
            };

            visit.Begin(plan);
            Assert.That(visit.Current, Is.SameAs(plan));
            Assert.That(visit.DoneGoals, Is.EqualTo(new[] { false, false }));

            visit.ReportProgress(new[] { true, false });
            Assert.That(visit.DoneGoals, Is.EqualTo(new[] { true, false }));

            visit.End();
            Assert.That(visit.Current, Is.Null);
            Assert.That(visit.DoneGoals, Is.Empty);
            Assert.That(changes, Is.EqualTo(3), "시작·진행·끝을 알린다");
        }

        [Test]
        public void Start_IsAnnouncedOnce()
        {
            var visit = new PracticeVisit();

            Assert.That(visit.TakeAnnouncement(), Is.False, "방문 전");
            visit.Begin(new PracticeVisitPlan());
            Assert.That(visit.TakeAnnouncement(), Is.True);
            Assert.That(visit.TakeAnnouncement(), Is.False, "한 번만");

            visit.Begin(new PracticeVisitPlan());
            visit.End();
            Assert.That(visit.TakeAnnouncement(), Is.False, "끝난 방문은 알리지 않는다");
        }
    }
}
