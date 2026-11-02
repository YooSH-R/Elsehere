using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    /// <summary>걷기·둘러보기처럼 해내면 넘어가는 단계는 완료 표시를 잠깐 보인 뒤 저절로 넘어가는지.</summary>
    public class TutorialAutoAdvanceTests
    {
        private TutorialAutoAdvance autoAdvance;

        [SetUp]
        public void Create()
        {
            autoAdvance = new TutorialAutoAdvance();
        }

        [Test]
        public void DoneStep_AdvancesAfterShowingTheCheck()
        {
            Assert.That(autoAdvance.Tick(TutorialStep.Walk, true, 0.5f), Is.False);
            Assert.That(autoAdvance.Tick(TutorialStep.Walk, true, 0.6f), Is.True);
        }

        [Test]
        public void NotDoneStep_DoesNotAdvance()
        {
            Assert.That(autoAdvance.Tick(TutorialStep.Look, false, 5f), Is.False);
        }

        [Test]
        public void Arrival_WaitsForTheStartButton()
        {
            // 도착 인사는 할 일이 없으므로 [시작하기]를 눌러야 넘어간다.
            Assert.That(autoAdvance.Tick(TutorialStep.Arrival, true, 5f), Is.False);
        }

        [Test]
        public void NewStep_StartsCountingAgain()
        {
            autoAdvance.Tick(TutorialStep.Walk, true, 0.9f);

            Assert.That(autoAdvance.Tick(TutorialStep.Look, true, 0.5f), Is.False);
            Assert.That(autoAdvance.Tick(TutorialStep.Look, true, 0.6f), Is.True);
        }
    }
}
