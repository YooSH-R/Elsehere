using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 알림 카드는 오른쪽 위 아이콘 줄과 겹치지 않도록 화면 위 가운데, 상단 줄 아래에 크게 띄운다.
    /// 열려 있는 창 위에서도 보이게 따로 높은 순서의 캔버스에 둔다.
    /// </summary>
    public sealed class HudNoticeTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private GameObject instance;

        [TearDown]
        public void DestroyHud() => Object.DestroyImmediate(instance);

        [Test]
        public void Notice_AppearsAtTheTopCenterBelowTheTopBar()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            var hud = instance.GetComponent<LearningHudView>();
            var card = (RectTransform)instance.transform.Find("Notice/MessageCard");
            Assert.That(card, Is.Not.Null, "알림 카드가 화면 가운데 층에 없습니다.");
            Assert.That(instance.transform.Find("Panel/MessageCard"), Is.Null);
            Assert.That(card.gameObject.activeSelf, Is.False);

            hud.ShowMessage("피드백이 완성됐어요. 휴대폰에서 확인해 주세요.");

            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(card.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(card.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(card.anchoredPosition.x, Is.EqualTo(0f));
            Assert.That(
                -card.anchoredPosition.y,
                Is.GreaterThanOrEqualTo(HudTopBarLayout.Margin + HudTopBarLayout.Size),
                "알림 카드가 상단 아이콘 줄과 겹칩니다."
            );
        }

        [Test]
        public void Notice_IsLargeAndCentered()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            var card = (RectTransform)instance.transform.Find("Notice/MessageCard");
            Assert.That(card, Is.Not.Null);
            var message = card.Find("Message").GetComponent<TMP_Text>();

            Assert.That(card.sizeDelta.x, Is.GreaterThanOrEqualTo(600f));
            Assert.That(message.fontSize, Is.GreaterThanOrEqualTo(28f));
            Assert.That(message.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        [Test]
        public void Notice_DrawsAboveOpenWindowsAndBelowSessionNotice()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Transform layer = hud.transform.Find("Notice");
            Assert.That(layer, Is.Not.Null);
            var canvas = layer.GetComponent<Canvas>();

            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.GreaterThan(1500));
            Assert.That(canvas.sortingOrder, Is.LessThan(2000));
        }
    }
}
