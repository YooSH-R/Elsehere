using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 화면은 한 가지 디자인만 쓴다. 창은 뒤 화면을 흐리게 비추는 유리, 버튼은 입체 재질로 그리고,
    /// 제목·버튼 이름은 둥근 제목 글꼴, 본문은 읽기 쉬운 서비스 글꼴로 쓴다.
    /// </summary>
    public class GlassPopThemeTests
    {
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private UiThemeCatalog catalog;
        private GameObject target;

        [SetUp]
        public void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
        }

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(target);
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
        }

        [TestCase(UiThemeId.Jelly)]
        [TestCase(UiThemeId.Sticker)]
        [TestCase(UiThemeId.Cloud)]
        public void EveryThemeChoice_PaintsTheSameSingleDesign(UiThemeId saved)
        {
            Assert.That(
                catalog.Themes,
                Has.Length.EqualTo(1),
                "테마 목록에는 디자인 하나만 둡니다."
            );
            Assert.That(catalog.Get(saved), Is.SameAs(catalog.Themes[0]));
        }

        [TestCase(UiThemeRole.Panel)]
        [TestCase(UiThemeRole.Card)]
        [TestCase(UiThemeRole.Input)]
        public void Surfaces_UseTheGlassMaterial(UiThemeRole role)
        {
            Image image = Create(role).GetComponent<Image>();

            Assert.That(image.material.shader.name, Is.EqualTo("A305/UI/Glass"));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
        }

        [Test]
        public void Panel_DrawsItsShadowOutsideTheBoxSoTheGlassFitsTheBox()
        {
            GameObject panel = Create(UiThemeRole.Panel);

            Assert.That(
                panel.GetComponent<SurfaceBleed>().Margin,
                Is.EqualTo(catalog.Themes[0].PanelBleed)
            );
            Assert.That(catalog.Themes[0].PanelBleed.x, Is.GreaterThan(0f));
        }

        [Test]
        public void PillButton_UsesThePopMaterialAndTheRoundDisplayFont()
        {
            GameObject pill = Create(UiThemeRole.PillButton, withLabel: true);

            Assert.That(pill.GetComponent<Image>().material.shader.name, Is.EqualTo("A305/UI/Pop"));
            Assert.That(
                pill.transform.Find("Label").GetComponent<TMP_Text>().font.name,
                Does.StartWith("Jua")
            );
            Assert.That(
                pill.GetComponent<PillContentLayout>(),
                Is.Not.Null,
                "아이콘과 이름을 한 묶음으로 둡니다."
            );
        }

        [Test]
        public void BodyText_KeepsTheReadableServiceFont()
        {
            target = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            Connect(target.AddComponent<ThemedElement>(), UiThemeRole.Body);

            Assert.That(
                target.GetComponent<TMP_Text>().font.name,
                Does.StartWith("NanumSquareRound")
            );
        }

        private GameObject Create(UiThemeRole role, bool withLabel = false)
        {
            target = new GameObject(role.ToString(), typeof(RectTransform), typeof(Image));
            if (withLabel)
            {
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(target.transform, false);
            }
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
