using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 대화창처럼 장면 위 어두운 그라데이션에 놓이는 조작. 바탕은 클릭을 막지 않는 그라데이션, 버튼·입력칸은 반투명한 흰 테두리,
    /// 글자는 흰색으로 칠한다. 다음에 누를 버튼은 강조색 입체 버튼으로 바뀐다.
    /// </summary>
    public class OnDarkThemeTests
    {
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private UiThemeCatalog catalog;
        private UiThemeDefinition design;
        private GameObject target;

        [SetUp]
        public void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            design = catalog.Themes[0];
        }

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(target);
        }

        [Test]
        public void Scrim_PaintsTheDarkGradientWithoutBlockingTheScene()
        {
            Image scrim = Create(UiThemeRole.Scrim).GetComponent<Image>();

            Assert.That(scrim.sprite, Is.Not.Null.And.SameAs(design.ScrimSprite));
            Assert.That(scrim.color, Is.EqualTo(design.ScrimColor));
            Assert.That(
                design.ScrimColor.grayscale,
                Is.LessThan(.25f),
                "흰 글자가 읽히는 어두운 색이어야 합니다."
            );
            Assert.That(
                scrim.raycastTarget,
                Is.False,
                "투명한 위쪽이 장면 클릭을 막으면 안 됩니다."
            );
            Assert.That(
                scrim.material,
                Is.SameAs(scrim.defaultMaterial),
                "유리처럼 뒤 화면을 비추지 않습니다."
            );
        }

        [Test]
        public void GhostControl_IsATranslucentOutlineWithAWhiteLabel()
        {
            GameObject button = Create(UiThemeRole.GhostControl, withLabel: true);
            Image image = button.GetComponent<Image>();

            Assert.That(image.sprite, Is.Not.Null.And.SameAs(design.GhostSprite));
            Assert.That(image.material, Is.SameAs(image.defaultMaterial));
            TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
            Assert.That(label.color, Is.EqualTo(Color.white));
            Assert.That(label.font.name, Does.StartWith("Jua"));
            Assert.That(button.GetComponent<PillContentLayout>(), Is.Not.Null);
        }

        [Test]
        public void GhostControl_Selected_TurnsIntoThePrimaryPopButtonUntilDeselected()
        {
            GameObject button = Create(UiThemeRole.GhostControl, withLabel: true);
            ThemedElement element = button.GetComponent<ThemedElement>();
            Image image = button.GetComponent<Image>();
            TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();

            element.SetSelected(true);
            Assert.That(image.color, Is.EqualTo(design.ToneColor(UiTone.Primary)));
            Assert.That(image.material.shader.name, Is.EqualTo("A305/UI/Pop"));
            Assert.That(label.color, Is.EqualTo(design.ToneInk(UiTone.Primary)));

            element.SetSelected(false);
            Assert.That(image.sprite, Is.SameAs(design.GhostSprite));
            Assert.That(label.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void GhostInput_WritesWhiteTextOnATranslucentField()
        {
            target = new GameObject("Input", typeof(RectTransform), typeof(Image));
            var input = target.AddComponent<TMP_InputField>();
            input.textComponent = Child("Text");
            input.placeholder = Child("Placeholder");
            Connect(target.AddComponent<ThemedElement>(), UiThemeRole.GhostInput);

            Assert.That(target.GetComponent<Image>().sprite, Is.SameAs(design.GhostSprite));
            Assert.That(input.textComponent.color, Is.EqualTo(Color.white));
            Color placeholder = ((TMP_Text)input.placeholder).color;
            Assert.That(placeholder.a, Is.InRange(.4f, .8f), "안내 글은 입력한 글보다 흐립니다.");
            Assert.That(input.caretColor, Is.EqualTo(Color.white));
            Assert.That(input.customCaretColor, Is.True);
        }

        [TestCase(UiThemeRole.TitleOnDark, "Jua")]
        [TestCase(UiThemeRole.BodyOnDark, "NanumSquareRound")]
        public void TextOnDark_IsLightInItsFont(UiThemeRole role, string font)
        {
            target = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            Connect(target.AddComponent<ThemedElement>(), role);

            TMP_Text text = target.GetComponent<TMP_Text>();
            Assert.That(text.font.name, Does.StartWith(font));
            Assert.That(text.color.grayscale, Is.GreaterThan(.85f));
        }

        [Test]
        public void AccentOnDark_UsesThePrimaryColor()
        {
            target = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            Connect(target.AddComponent<ThemedElement>(), UiThemeRole.AccentOnDark);

            Assert.That(
                target.GetComponent<TMP_Text>().color,
                Is.EqualTo(design.ToneColor(UiTone.Primary))
            );
        }

        private TMP_Text Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(target.transform, false);
            return child.GetComponent<TMP_Text>();
        }

        private GameObject Create(UiThemeRole role, bool withLabel = false)
        {
            target = new GameObject(role.ToString(), typeof(RectTransform), typeof(Image));
            if (withLabel)
                Child("Label");
            Connect(target.AddComponent<ThemedElement>(), role);
            return target;
        }

        private void Connect(ThemedElement element, UiThemeRole role)
        {
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue = catalog;
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
        }
    }
}
