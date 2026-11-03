using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 사물 단어 카드의 버튼은 한 덩어리로 보인다. [저장]·[발음]은 같은 너비로 한 줄을 채우고,
    /// 그 아래 [알아요 · 호버에서 숨기기]도 같은 양 끝을 가진 버튼이다. 오른쪽 위 닫기 버튼에는 X가 보인다.
    /// </summary>
    public class WordTooltipLayoutTests
    {
        private const string PrefabPath = "Assets/Prefabs/ObjectHover/WordTooltip.prefab";

        private GameObject instance;
        private RectTransform panel;

        [SetUp]
        public void Open()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            panel = (RectTransform)instance.transform.Find("Panel");
            panel.gameObject.SetActive(true);
            foreach (string row in new[] { "WordActions", "Actions" })
                panel.Find(row).gameObject.SetActive(true);
            panel.Find("Actions/HideButton").gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        [TearDown]
        public void Close() => Object.DestroyImmediate(instance);

        [Test]
        public void SaveAndPronunciation_ShareTheRowEqually()
        {
            Rect save = Edges("WordActions/SaveButton");
            Rect say = Edges("WordActions/PronunciationButton");

            Assert.That(save.width, Is.EqualTo(say.width).Within(.5f));
            Assert.That(save.height, Is.EqualTo(say.height).Within(.5f));
        }

        [Test]
        public void HideButton_LinesUpWithTheButtonsAbove()
        {
            Rect save = Edges("WordActions/SaveButton");
            Rect say = Edges("WordActions/PronunciationButton");
            Rect hide = Edges("Actions/HideButton");

            Assert.That(hide.xMin, Is.EqualTo(save.xMin).Within(.5f), "왼쪽 끝");
            Assert.That(hide.xMax, Is.EqualTo(say.xMax).Within(.5f), "오른쪽 끝");
            Assert.That(hide.height, Is.EqualTo(save.height).Within(.5f), "같은 높이");
            Assert.That(
                panel.Find("Actions/HideButton").GetComponent<Image>().color.a,
                Is.GreaterThan(0f),
                "글자만이 아니라 버튼 바탕이 보입니다."
            );
        }

        [Test]
        public void CloseButton_ShowsAnX()
        {
            Transform close = panel.Find("CloseButton");
            close.GetComponent<PillContentLayout>().Arrange();
            Image icon = close.Find("Icon").GetComponent<Image>();

            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.enabled, Is.True);
        }

        // 카드 안에서의 버튼 가장자리(카드 기준 좌표)
        private Rect Edges(string path)
        {
            var target = (RectTransform)panel.Find(path);
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 min = panel.InverseTransformPoint(corners[0]);
            Vector3 max = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
