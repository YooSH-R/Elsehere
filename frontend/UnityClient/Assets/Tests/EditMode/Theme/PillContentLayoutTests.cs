using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 알약 버튼의 아이콘과 이름은 한 묶음으로 가운데에 놓인다. 아이콘은 이름과 정해진 간격을 두고,
    /// 낮은 버튼은 아이콘 없이 이름만 가운데에 둔다.
    /// </summary>
    public class PillContentLayoutTests
    {
        private const string FontPath = "Assets/Art/UI/Fonts/NanumSquareRound SDF.asset";
        private GameObject target;
        private Texture2D texture;

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Arrange_KeepsIconAndNameTogetherAtTheCenter()
        {
            PillContentLayout layout = Create(new Vector2(240, 70), withIcon: true);

            layout.Arrange();

            RectTransform icon = Rect("Icon");
            RectTransform label = Rect("Label");
            float iconRight = icon.anchoredPosition.x + icon.sizeDelta.x / 2f;
            float labelLeft = label.anchoredPosition.x - label.sizeDelta.x / 2f;
            float groupLeft = icon.anchoredPosition.x - icon.sizeDelta.x / 2f;
            float groupRight = label.anchoredPosition.x + label.sizeDelta.x / 2f;
            Assert.That(labelLeft - iconRight, Is.EqualTo(PillContentLayout.Gap).Within(.01f));
            Assert.That(
                groupLeft + groupRight,
                Is.EqualTo(0f).Within(.01f),
                "묶음이 가운데에 있어야 합니다."
            );
            Assert.That(icon.anchoredPosition.y, Is.EqualTo(label.anchoredPosition.y).Within(.01f));
        }

        [Test]
        public void Arrange_LowButton_DropsTheIconAndCentersTheName()
        {
            PillContentLayout layout = Create(new Vector2(120, 58), withIcon: true);

            layout.Arrange();

            Assert.That(target.transform.Find("Icon").GetComponent<Image>().enabled, Is.False);
            Assert.That(Rect("Label").anchoredPosition.x, Is.EqualTo(0f).Within(.01f));
        }

        [Test]
        public void Arrange_AfterTheThemeTurnsTheIconBackOn_KeepsItOffOnALowButton()
        {
            PillContentLayout layout = Create(new Vector2(120, 58), withIcon: true);
            layout.Arrange();
            Image icon = target.transform.Find("Icon").GetComponent<Image>();

            // 테마가 버튼을 다시 칠하면 아이콘을 켠다. 이름·크기는 그대로다.
            icon.enabled = true;
            layout.Arrange(force: false);

            Assert.That(icon.enabled, Is.False);
        }

        [Test]
        public void Arrange_IconOnlyButton_CentersTheIconWithoutTheNameGap()
        {
            // 대화창 [보내기]처럼 이름을 숨긴 둥근 버튼은 아이콘만 가운데에 둔다.
            PillContentLayout layout = Create(new Vector2(72, 72), withIcon: true);
            target.transform.Find("Label").gameObject.SetActive(false);

            layout.Arrange();

            Assert.That(target.transform.Find("Icon").GetComponent<Image>().enabled, Is.True);
            Assert.That(Rect("Icon").anchoredPosition.x, Is.EqualTo(0f).Within(.01f));
        }

        // 단어 카드 닫기처럼 낮은 둥근 버튼도 이름이 없으면 아이콘(X)이 보여야 한다. 아이콘을 지우면 빈 조약돌만 남는다.
        [Test]
        public void Arrange_ShortIconOnlyButton_StillShowsItsIcon()
        {
            PillContentLayout layout = Create(new Vector2(44, 44), withIcon: true);
            target.transform.Find("Label").gameObject.SetActive(false);

            layout.Arrange();

            Assert.That(target.transform.Find("Icon").GetComponent<Image>().enabled, Is.True);
            Assert.That(Rect("Icon").anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(Rect("Icon").sizeDelta.x, Is.GreaterThan(0f).And.LessThanOrEqualTo(44f));
        }

        private PillContentLayout Create(Vector2 size, bool withIcon)
        {
            target = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            ((RectTransform)target.transform).sizeDelta = size;
            if (withIcon)
            {
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(target.transform, false);
                texture = new Texture2D(8, 8);
                icon.GetComponent<Image>().sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, 8, 8),
                    Vector2.one / 2f
                );
            }
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(target.transform, false);
            var text = label.GetComponent<TMP_Text>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.fontSize = 22;
            text.text = "담기";
            return target.AddComponent<PillContentLayout>();
        }

        private RectTransform Rect(string name) => (RectTransform)target.transform.Find(name);
    }
}
