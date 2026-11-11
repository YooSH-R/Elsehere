using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 아래 두께가 있는 버튼(시안 .btn의 0 5px 0 그림자, 탭은 3px, 유리 버튼은 4px)은 앞면 가운데에 글자를 두면 떠 보인다.
    /// 글자 모양을 가운데(Midline)에 맞추고 두께의 절반만큼 내려, 두께까지 포함한 버튼 가운데에 둔다.
    /// </summary>
    public class LippedButtonLabelTests
    {
        private const string FeedbackPrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const float Px = 1.2f;

        private GameObject instance;

        [SetUp]
        public void LoadScreen() =>
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackPrefabPath)
            );

        [TearDown]
        public void DestroyScreen() => Object.DestroyImmediate(instance);

        [Test]
        public void Tabs_LabelsSitHalfTheLipLower()
        {
            Transform tabs = instance.transform.Find("Panel/Sections");
            for (int i = 0; i < tabs.childCount; i++)
                AssertLowered(tabs.GetChild(i).Find("Label").GetComponent<TMP_Text>(), 1.5f * Px);
        }

        // 쪽 번호는 양옆 둥근 유리 버튼(두께 4px)의 화살표와 같은 높이에 둔다.
        [Test]
        public void PageNumber_SitsWithTheArrows()
        {
            AssertLowered(Find("Panel/Navigation/PageNumber"), 2f * Px);
        }

        // 대화 노트 줄의 [피드백 보기]는 피드백 화면 탭과 같은 흰 알약(두께 3), [다시 연습]은 코랄 알약(두께 5)이다.
        [Test]
        public void ConversationNoteButtons_LabelsSitHalfTheLipLower()
        {
            AssertLowered(HudText("History/Rows/Row0/OpenButton/Label"), 1.5f * Px);
            AssertLowered(HudText("History/Rows/Row0/PracticeButton/Label"), 2.5f * Px);
        }

        [Test]
        public void FeedbackListPageNumber_SitsWithTheArrows()
        {
            AssertLowered(HudText("History/Pager/PageNumber"), 2f * Px);
        }

        private static TMP_Text HudText(string path) =>
            AssetDatabase
                .LoadAssetAtPath<GameObject>(HudPrefabPath)
                .transform.Find(path)
                .GetComponent<TMP_Text>();

        private TMP_Text Find(string path) =>
            instance.transform.Find(path).GetComponent<TMP_Text>();

        private static void AssertLowered(TMP_Text label, float drop)
        {
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Midline), label.name);
            var rect = label.rectTransform;
            var box = (RectTransform)rect.parent;
            float labelCentre = rect.localPosition.y + rect.rect.center.y;
            Assert.That(box.rect.center.y - labelCentre, Is.EqualTo(drop).Within(.05f), label.name);
        }
    }
}
