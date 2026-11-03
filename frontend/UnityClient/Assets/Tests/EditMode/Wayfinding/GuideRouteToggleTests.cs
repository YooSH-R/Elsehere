using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Wayfinding.Tests
{
    public class GuideRouteToggleTests
    {
        private bool saved;
        private GameObject chip;

        [SetUp]
        public void RememberCurrent()
        {
            saved = GuideRoutePreference.ShowsRoute;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            GuideRoutePreference.ShowsRoute = saved;
            Object.DestroyImmediate(chip);
        }

        [Test]
        public void Label_SaysWhetherTheRouteIsOn()
        {
            Assert.That(GuideRouteToggle.Label(true), Does.Contain("길 안내 켬"));
            Assert.That(GuideRouteToggle.Label(false), Does.Contain("길 안내 끔"));
        }

        [Test]
        public void Toggle_TurnsTheRouteOffAndSaysSo()
        {
            GuideRoutePreference.ShowsRoute = true;
            GuideRouteToggle toggle = CreateToggle(out TMP_Text label);

            toggle.Toggle();

            Assert.That(GuideRoutePreference.ShowsRoute, Is.False);
            Assert.That(label.text, Is.EqualTo(GuideRouteToggle.Label(false)));
        }

        [Test]
        public void Toggle_Twice_TurnsTheRouteBackOn()
        {
            GuideRoutePreference.ShowsRoute = true;
            GuideRouteToggle toggle = CreateToggle(out TMP_Text label);

            toggle.Toggle();
            toggle.Toggle();

            Assert.That(GuideRoutePreference.ShowsRoute, Is.True);
            Assert.That(label.text, Is.EqualTo(GuideRouteToggle.Label(true)));
        }

        private GuideRouteToggle CreateToggle(out TMP_Text label)
        {
            chip = new GameObject("RouteToggle", typeof(RectTransform), typeof(Button));
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(chip.transform, false);
            label = text.GetComponent<TMP_Text>();
            var toggle = chip.AddComponent<GuideRouteToggle>();
            var fields = new SerializedObject(toggle);
            fields.FindProperty("button").objectReferenceValue = chip.GetComponent<Button>();
            fields.FindProperty("label").objectReferenceValue = label;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return toggle;
        }
    }
}
