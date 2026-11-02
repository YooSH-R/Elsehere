using NUnit.Framework;
using UnityEditor;

namespace A305.Tutorial.Tests
{
    /// <summary>튜토리얼에서 길을 묻는 NPC가 마을 행인 Ben이고, 그동안 튜토리얼 대본으로 말하는지. 안내 문구가 한눈에 읽히는 한 줄인지.</summary>
    public class TutorialGuideAssetTests
    {
        private const string GuidePath = "Assets/Data/Tutorial/TutorialGuide.asset";

        [Test]
        public void TutorialNpc_IsBenTheTownPasserbyUsingTheTutorialScript()
        {
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);

            Assert.That(guide.TutorialNpcCode, Is.EqualTo("TOWN_PASSERBY"));
            Assert.That(guide.TutorialScriptKey, Is.EqualTo("TUTORIAL_PASSERBY"));
        }

        [TestCase(TutorialStep.Arrival)]
        [TestCase(TutorialStep.Walk)]
        [TestCase(TutorialStep.Look)]
        [TestCase(TutorialStep.SaveWord)]
        [TestCase(TutorialStep.OpenVocabulary)]
        [TestCase(TutorialStep.LeaveLodging)]
        [TestCase(TutorialStep.MeetBen)]
        [TestCase(TutorialStep.BenFeedback)]
        [TestCase(TutorialStep.TodayGoal)]
        public void StepGuide_IsOneShortLineForEachDevice(TutorialStep step)
        {
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);

            Assert.That(guide.TryGetStepGuide(step, out TutorialStepGuide stepGuide), Is.True);
            foreach (string line in new[] { stepGuide.Task, stepGuide.TouchTask })
            {
                Assert.That(line, Does.Not.Contain("\n"));
                Assert.That(
                    TutorialGuideText.VisibleLength(line),
                    Is.InRange(1, MaxLineLength),
                    line
                );
            }
        }

        [TestCase(TutorialStep.Walk)]
        [TestCase(TutorialStep.Look)]
        public void ActionSteps_ListEveryRequiredActionInOrder(TutorialStep step)
        {
            // 걷기·둘러보기는 " · "로 나눈 할 일마다 해냈는지 색으로 보이므로, 할 일 수가 필요한 행동 수와 같아야 한다.
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);
            guide.TryGetStepGuide(step, out TutorialStepGuide stepGuide);

            Assert.That(
                TutorialGuideText.PartCount(stepGuide.Task),
                Is.EqualTo(TutorialProgress.RequiredActions(step, false).Count)
            );
            Assert.That(
                TutorialGuideText.PartCount(stepGuide.TouchTask),
                Is.EqualTo(TutorialProgress.RequiredActions(step, true).Count)
            );
        }

        // 한 줄 안내가 카드 한 줄에 들어가는 길이(보이는 글자 수).
        private const int MaxLineLength = 34;
    }
}
