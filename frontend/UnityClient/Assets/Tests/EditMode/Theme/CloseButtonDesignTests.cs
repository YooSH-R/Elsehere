using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 창 오른쪽 위 닫기 X는 어느 화면이든 같은 흰 원 바탕에 같은 진회색 X를 쓴다. 창은 52, 작은 카드는 44이고 오른쪽 위 안쪽 20에 둔다.
    /// 여러 Builder가 각자 X를 만들어 모양이 네 가지로 갈렸던 것을 막는다(S15P31A305-525).
    /// </summary>
    public class CloseButtonDesignTests
    {
        private const string Hud = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string Feedback = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string Vocabulary = "Assets/Prefabs/Learning/VocabularyScreen.prefab";
        private const string Mission = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string WordCard = "Assets/Prefabs/ObjectHover/WordTooltip.prefab";

        private static readonly object[] Buttons =
        {
            new object[] { Hud, "ControlsGuide/CloseButton", 52f },
            new object[] { Hud, "MyPage/Window/CloseButton", 52f },
            new object[] { Hud, "History/CloseButton", 52f },
            new object[] { Feedback, "Panel/TopCloseButton", 52f },
            new object[] { Vocabulary, "Panel/CloseButton", 52f },
            new object[] { Mission, "Content/Journal/CloseButton", 52f },
            new object[] { Mission, "Content/MapOverlay/Panel/CloseButton", 52f },
            new object[] { WordCard, "Panel/CloseButton", 44f },
        };

        [TestCaseSource(nameof(Buttons))]
        public void CornerClose_IsAWhiteCircleWithTheSharedX(
            string prefabPath,
            string buttonPath,
            float size
        )
        {
            var button = (RectTransform)Load(prefabPath).transform.Find(buttonPath);
            Assert.That(button, Is.Not.Null, buttonPath);

            Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(size, size)), "크기");
            Assert.That(button.anchorMin, Is.EqualTo(Vector2.one), "오른쪽 위에 붙임");
            Assert.That(button.anchorMax, Is.EqualTo(Vector2.one), "오른쪽 위에 붙임");
            Assert.That(button.anchoredPosition, Is.EqualTo(new Vector2(-20, -20)), "안쪽 20");

            var face = button.GetComponent<Image>();
            Assert.That(face.sprite.name, Is.EqualTo("CloseButtonFace"), "흰 원 바탕");
            Assert.That(face.color, Is.EqualTo(Color.white), "흰 원 바탕");
            Assert.That(button.GetComponent<Button>().targetGraphic, Is.EqualTo(face));

            var icon = button.Find("Icon").GetComponent<Image>();
            Assert.That(icon.sprite.name, Is.EqualTo("Close"), "공용 X 아이콘");
            Assert.That(
                icon.color,
                Is.EqualTo(Color.white),
                "진회색으로 그린 X 그림을 그대로 쓴다"
            );
            Assert.That(
                icon.rectTransform.sizeDelta.y,
                Is.GreaterThanOrEqualTo(size / 2f),
                "아이콘 칸은 버튼 높이의 절반 이상(S15P31A305-515)"
            );
            Assert.That(
                button
                    .Cast<Transform>()
                    .Where(child => child.gameObject.activeSelf)
                    .Select(c => c.name),
                Is.EqualTo(new[] { "Icon" }),
                "X 말고 다른 그림·글자는 없다"
            );
        }

        [Test]
        public void EveryCornerClose_UsesTheSameXColor()
        {
            Color[] colors = Buttons
                .Cast<object[]>()
                .Select(row =>
                    Load((string)row[0])
                        .transform.Find((string)row[1] + "/Icon")
                        .GetComponent<Image>()
                        .color
                )
                .Distinct()
                .ToArray();

            Assert.That(colors.Length, Is.EqualTo(1));
        }

        // 닫기는 할 일이 아니므로 강조색 대신 흰 보조 버튼으로 둔다.
        [Test]
        public void FeedbackBottomClose_IsAWhiteSecondaryButton()
        {
            var button = Load(Feedback).transform.Find("Panel/Buttons/CloseButton");

            var face = button.GetComponent<Image>();
            Assert.That(face.sprite.name, Is.EqualTo("CloseButtonFace"), "흰 알약 바탕");
            Assert.That(face.color, Is.EqualTo(Color.white), "흰 알약 바탕");
            TMP_Text label = button.GetComponentsInChildren<TMP_Text>(true).Single();
            Assert.That(label.gameObject.activeSelf, Is.True);
            Assert.That(label.text, Is.EqualTo("닫기"));
            Assert.That(label.color.maxColorComponent, Is.LessThan(.35f), "진회색 글자");
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }
    }
}
