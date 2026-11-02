using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>하나를 고르는 캐릭터 버튼·언어 카드가 선택 표시를 고정 색 대신 지금 테마의 색으로 칠하는지 확인한다.</summary>
    public class ThemedElementTests
    {
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";

        private UiThemeCatalog catalog;
        private GameObject target;

        [SetUp]
        public void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
        }

        [TearDown]
        public void RestoreTheme()
        {
            Object.DestroyImmediate(target);
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
        }

        [Test]
        public void SetSelected_Card_LeansTowardThePrimaryColorUntilDeselected()
        {
            UiThemeDefinition jelly = catalog.Get(UiThemeId.Jelly);
            Color primary = jelly.ToneColor(UiTone.Primary);
            ThemedElement element = Create(UiThemeRole.Card, out Image image);

            element.SetSelected(true);
            Assert.That(
                Distance(image.color, primary),
                Is.LessThan(Distance(jelly.CardTint, primary)),
                "고른 카드는 기본 카드보다 강조색에 가까워야 합니다."
            );

            element.SetSelected(false);
            Assert.That(image.color, Is.EqualTo(jelly.CardTint));
        }

        [Test]
        public void SetSelected_ThenThemeChanges_LeansTowardTheNewThemePrimary()
        {
            ThemedElement element = Create(UiThemeRole.Card, out Image image);
            element.SetSelected(true);

            UiThemeSelection.Shared.Select(UiThemeId.Sticker);
            element.ApplyCurrent();

            UiThemeDefinition sticker = catalog.Get(UiThemeId.Sticker);
            Color primary = sticker.ToneColor(UiTone.Primary);
            Assert.That(
                Distance(image.color, primary),
                Is.LessThan(Distance(sticker.CardTint, primary))
            );
        }

        [Test]
        public void Backdrop_PaintsALightTintOfTheThemeThatFollowsThemeChanges()
        {
            // 입구 화면 배경. 어두운 빈 화면 대신 밝은 테마 색으로 칠하되, 흰 창과 구분되게 흰색은 아니다.
            ThemedElement element = Create(UiThemeRole.Backdrop, out Image image);
            Color jelly = image.color;

            Assert.That(jelly.grayscale, Is.GreaterThan(.8f), "밝은 색이어야 합니다.");
            Assert.That(
                Distance(jelly, Color.white),
                Is.GreaterThan(.05f),
                "흰 창과 구분돼야 합니다."
            );

            UiThemeSelection.Shared.Select(UiThemeId.Cloud);
            element.ApplyCurrent();

            Assert.That(
                image.color,
                Is.Not.EqualTo(jelly),
                "테마를 바꾸면 배경색도 바뀌어야 합니다."
            );
        }

        private ThemedElement Create(UiThemeRole role, out Image image)
        {
            target = new GameObject("Choice", typeof(RectTransform), typeof(Image));
            image = target.GetComponent<Image>();
            var element = target.AddComponent<ThemedElement>();
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue = catalog;
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.FindProperty("tone").enumValueIndex = (int)UiTone.Neutral;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
            return element;
        }

        private static float Distance(Color a, Color b) =>
            Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));
    }
}
