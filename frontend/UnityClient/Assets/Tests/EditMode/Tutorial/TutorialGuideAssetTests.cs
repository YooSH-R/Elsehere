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
        public void Look_TellsToDragWithTheRightMouseButton()
        {
            // 시점은 마우스 오른쪽 버튼을 누른 채 끌어 돌린다(Player/CameraRotate). 왼쪽 클릭은 사물 카드 고정에 쓴다.
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);
            guide.TryGetStepGuide(TutorialStep.Look, out TutorialStepGuide stepGuide);

            Assert.That(stepGuide.Task, Does.Contain("{마우스 오른쪽}"));
            Assert.That(stepGuide.Task, Does.Not.Contain("왼쪽"));
        }

        [Test]
        public void SkipButton_LabelStartsAfterItsIcon_AndHasRoomForTheWord()
        {
            // 알약 버튼은 아이콘을 왼쪽 끝에 둔다. 글자를 버튼 전체 가운데에 두면 좁은 버튼에서 아이콘 위로 겹치므로,
            // 글자 칸을 아이콘 오른쪽부터 잡는다.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuidePrefabPath);
            Transform button = prefab.transform.Find("Panel/Content/Header/SkipButton");
            var icon = (RectTransform)button.Find("Icon");
            var labelRect = (RectTransform)button.Find("Label");
            TMP_Text label = labelRect.GetComponent<TMP_Text>();

            float iconRight = icon.anchoredPosition.x + icon.sizeDelta.x;
            Assert.That(
                labelRect.offsetMin.x,
                Is.GreaterThanOrEqualTo(iconRight + 4f),
                "글자 칸이 아이콘과 겹칩니다."
            );
            float labelRoom =
                button.GetComponent<LayoutElement>().preferredWidth
                - labelRect.offsetMin.x
                + labelRect.offsetMax.x;
            // 한글 한 글자는 글자 크기만큼 넓다.
            Assert.That(labelRoom, Is.GreaterThanOrEqualTo(label.text.Length * label.fontSize));
        }

        private const string GuidePrefabPath = "Assets/Prefabs/Tutorial/TutorialGuide.prefab";

        // 한 줄 안내가 카드 한 줄에 들어가는 길이(보이는 글자 수).
        private const int MaxLineLength = 34;
    }
}
