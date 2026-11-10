using A305.World;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class PracticePresentationTests
    {
        [Test]
        public void Briefing_ShowsTheSituationAndWhoToTalkTo()
        {
            var plan = new PracticeVisitPlan
            {
                Title = "못 먹는 재료 말하기",
                PlaceName = "식당",
                NpcName = "Rosa",
                Description = "알레르기가 있는 재료를 말하고 <빼> 달라고 해요.",
            };

            string briefing = PracticePresentation.Briefing(plan);

            Assert.That(briefing, Does.StartWith("<b>못 먹는 재료 말하기</b>"));
            Assert.That(briefing, Does.Contain("식당 · Rosa"));
            Assert.That(briefing, Does.Contain("알레르기가 있는 재료를 말하고 ‹빼› 달라고 해요."));
            Assert.That(briefing, Does.Contain("<b>이번에 할 일</b>"));
        }

        [TestCase(true, "연습 완료! ‘자리 잡기’ 목표를 모두 해냈어요.")]
        [TestCase(false, "‘자리 잡기’ 목표가 아직 남았어요. 장소 연습에서 다시 할 수 있어요.")]
        public void Result_SaysWhetherEveryGoalWasDone(bool done, string message)
        {
            var plan = new PracticeVisitPlan { Title = "자리 잡기" };

            Assert.That(PracticePresentation.Result(plan, done), Is.EqualTo(message));
        }
    }
}
