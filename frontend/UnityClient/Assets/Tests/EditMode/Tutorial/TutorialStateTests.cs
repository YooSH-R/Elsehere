using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    public class TutorialStateTests
    {
        private const string TutorialNpcCode = "TUTORIAL_PASSERBY";
        private const string MissionNpcCode = "CAFE_STAFF";

        [SetUp]
        [TearDown]
        public void ClearState()
        {
            TutorialState.Clear();
        }

        [Test]
        public void MarkFinishedOnServer_BeforeFirstUse_StartsCompleted()
        {
            TutorialState.MarkFinishedOnServer();

            TutorialProgress progress = TutorialState.GetOrCreate(TutorialNpcCode, MissionNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public void MarkFinishedOnServer_AfterFirstUse_CompletesSameProgress()
        {
            TutorialProgress progress = TutorialState.GetOrCreate(TutorialNpcCode, MissionNpcCode);

            TutorialState.MarkFinishedOnServer();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
        }

        [Test]
        public void Clear_ForgetsFinishedOnServer()
        {
            TutorialState.MarkFinishedOnServer();

            TutorialState.Clear();

            TutorialProgress progress = TutorialState.GetOrCreate(TutorialNpcCode, MissionNpcCode);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Welcome));
        }
    }
}
