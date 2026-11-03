using A305.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        [Test]
        public void VocabularyCaption_ExplainsTheBookAndTheNextStep()
        {
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);

            Assert.That(guide.VocabularyCaption, Does.Contain("{저장}"), "무엇이 모이는지");
            Assert.That(guide.VocabularyCaption, Does.Contain("{외웠어요}"), "무엇을 할 수 있는지");
            Assert.That(guide.VocabularyCaption, Does.Contain("밖으로"), "다음 할 일");
        }

        [Test]
        public void DialogueCaptions_SayTheTutorialFollowsAFixedFlow_AndExplainTheHelpCards()
        {
            // 튜토리얼 대화는 무엇을 말하든 정해진 흐름대로 진행되고, 왼쪽 [문장]·[단어] 카드가 무엇인지 알린다.
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);

            Assert.That(guide.GuidedLineCaption, Does.Contain("정해진 흐름"));
            Assert.That(guide.GuidedLineCaption, Does.Not.Contain("연습"));
            Assert.That(guide.GuidedLineMissCaption, Does.Contain("정해진 흐름"));
            Assert.That(guide.DialogueLinesCaption, Does.Contain("Ben"));
            Assert.That(guide.DialogueToolsCaption, Does.Contain("{뜻}"));
            Assert.That(guide.DialogueToolsCaption, Does.Contain("{예시 문장}"));
            Assert.That(guide.DialogueEndCaption, Does.Contain("{대화 종료}"));
            Assert.That(guide.FeedbackSectionsCaption, Does.Contain("{요약}"));
            Assert.That(guide.FeedbackPagesCaption, Does.Contain("화살표"));
            Assert.That(
                guide.FeedbackCloseCaption,
                Does.Contain("{X}"),
                "피드백은 오른쪽 위 X로 닫는다."
            );
            guide.TryGetStepGuide(TutorialStep.BenFeedback, out TutorialStepGuide feedback);
            Assert.That(feedback.Task, Does.Contain("{X}"));
            Assert.That(
                guide.DialogueEndCaption,
                Does.Contain("피드백"),
                "누르면 무엇이 이어지는지"
            );
            Assert.That(guide.SuggestedWordsCaption, Does.Contain("{단어}"));
            Assert.That(guide.SuggestedWordsCaption, Does.Contain("추천 단어"));
        }

        [Test]
        public void Walk_TellsToJumpWithTheSpaceBar()
        {
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);
            guide.TryGetStepGuide(TutorialStep.Walk, out TutorialStepGuide stepGuide);

            Assert.That(stepGuide.Task, Does.Contain("{스페이스} 점프"));
        }

        [Test]
        public void Look_TellsToDragWithTheLeftMouseButton()
        {
            // 시점은 마우스 왼쪽 버튼을 누른 채 끌어 돌린다(Player/CameraRotate).
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);
            guide.TryGetStepGuide(TutorialStep.Look, out TutorialStepGuide stepGuide);

            Assert.That(stepGuide.Task, Does.Contain("{마우스 왼쪽}"));
            Assert.That(stepGuide.Task, Does.Not.Contain("오른쪽"));
        }

        [Test]
        public void SkipButton_ShowsOnlyItsName_AndTheNameFits()
        {
            // 알약 버튼은 아이콘과 이름을 한 묶음으로 가운데에 두고, 큰 주요 버튼보다 낮은 버튼은 아이콘 없이 이름만 둔다.
            // 좁은 [건너뛰기]에서 이름이 아이콘과 겹치거나 잘리지 않아야 한다.
            var guide = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(GuidePrefabPath)
            );
            try
            {
                var button = (RectTransform)guide.transform.Find("Panel/Content/Header/SkipButton");
                // 머리줄은 버튼을 정한 크기로 놓는다.
                var size = button.GetComponent<LayoutElement>();
                button.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    size.preferredWidth
                );
                button.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.preferredHeight);
                button.GetComponent<PillContentLayout>().Arrange();
                Image icon = button.Find("Icon").GetComponent<Image>();
                TMP_Text label = button.Find("Label").GetComponent<TMP_Text>();

                Assert.That(icon.enabled, Is.False, "낮은 버튼에 아이콘이 보입니다.");
                Assert.That(
                    label.rectTransform.sizeDelta.x,
                    Is.GreaterThanOrEqualTo(label.GetPreferredValues(label.text).x - .5f),
                    "이름이 버튼 안에 다 들어가지 않습니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(guide);
            }
        }

        private const string GuidePrefabPath = "Assets/Prefabs/Tutorial/TutorialGuide.prefab";

        // 한 줄 안내가 카드 한 줄에 들어가는 길이(보이는 글자 수).
        private const int MaxLineLength = 34;
    }
}
