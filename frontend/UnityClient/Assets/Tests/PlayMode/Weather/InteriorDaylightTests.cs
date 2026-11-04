#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using A305.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace A305.Weather.PlayModeTests
{
    public class InteriorDaylightTests
    {
        private const double Midnight = 0;
        private const double Midday = 12 * 60 + 30;
        private const double Sunset = 19 * 60;
        private const float WindowLightIntensity = 1.4f;
        private const float FillIntensity = 2.1f;
        private const float LampIntensity = 1.8f;

        private static readonly Color DesignedWindowLight = new Color(1f, .96f, .88f);
        private static readonly Color DesignedWindow = new Color(.651f, .8f, .824f);
        private static readonly Color DesignedAmbientSky = new Color(.82f, .85f, .88f);

        private readonly SavedRenderSettings saved = new SavedRenderSettings();
        private readonly List<Object> created = new List<Object>();
        private DaylightSettings daylight;
        private Material designedWindow;
        private Light windowLight;
        private Light fill;
        private Light lamp;
        private Renderer window;
        private InteriorDaylight interior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return IsolatedScene.Open();
            saved.Read();
            designedWindow = Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            designedWindow.color = DesignedWindow;
            daylight = Keep(DaylightSettings.Create(windowMaterials: new[] { designedWindow }));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = DesignedAmbientSky;
            RenderSettings.ambientEquatorColor = new Color(.78f, .73f, .64f);
            RenderSettings.ambientGroundColor = new Color(.48f, .43f, .37f);

            windowLight = NewLight(
                "Directional Light",
                LightType.Directional,
                WindowLightIntensity
            );
            windowLight.color = DesignedWindowLight;
            fill = NewLight("RoomBounce", LightType.Point, FillIntensity);
            lamp = NewLight("WarmLight", LightType.Point, LampIntensity);
            GameObject glass = Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
            glass.name = "Glass";
            window = glass.GetComponent<Renderer>();
            window.sharedMaterial = designedWindow;
        }

        [TearDown]
        public void TearDown()
        {
            if (interior != null)
                Object.DestroyImmediate(interior.gameObject);
            foreach (Object item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            saved.Restore();
        }

        [UnityTest]
        public IEnumerator Midnight_TurnsWindowLightIntoWarmRoomLightAndBrightensLamps()
        {
            interior = NewInterior();
            yield return null;

            interior.ShowTime(Midnight);

            Assert.That(
                windowLight.intensity,
                Is.EqualTo(WindowLightIntensity * daylight.NightWindowLightScale).Within(.001f)
            );
            AssertColor(windowLight.color, daylight.NightRoomLightColor);
            Assert.That(
                fill.intensity,
                Is.EqualTo(FillIntensity * daylight.NightFillScale).Within(.001f)
            );
            Assert.That(
                lamp.intensity,
                Is.EqualTo(LampIntensity * daylight.NightLampScale).Within(.001f)
            );
        }

        [UnityTest]
        public IEnumerator Midnight_PaintsWindowsNightAndDimsRoom()
        {
            interior = NewInterior();
            yield return null;

            interior.ShowTime(Midnight);

            AssertColor(window.sharedMaterial.color, daylight.NightWindowColor);
            AssertColor(designedWindow.color, DesignedWindow, "원본 유리 재질은 바뀌면 안 됩니다.");
            AssertColor(
                RenderSettings.ambientSkyColor,
                DesignedAmbientSky * daylight.NightInteriorAmbientScale
            );
        }

        [UnityTest]
        public IEnumerator Midnight_KeepsRoomLitAsIfLightsAreOn()
        {
            interior = NewInterior();
            yield return null;

            interior.ShowTime(Midnight);

            // 밤의 실내는 불을 켠 방이다. 간접등만 켠 것처럼 어두우면 안 된다.
            Assert.That(windowLight.intensity, Is.GreaterThanOrEqualTo(WindowLightIntensity * .7f));
            Assert.That(
                RenderSettings.ambientSkyColor.grayscale,
                Is.GreaterThanOrEqualTo(DesignedAmbientSky.grayscale * .85f)
            );
            Assert.That(fill.intensity, Is.GreaterThanOrEqualTo(FillIntensity * .8f));
        }

        [UnityTest]
        public IEnumerator Midday_KeepsDesignedRoom()
        {
            interior = NewInterior();
            yield return null;
            interior.ShowTime(Midnight);

            interior.ShowTime(Midday);

            Assert.That(windowLight.intensity, Is.EqualTo(WindowLightIntensity).Within(.001f));
            AssertColor(windowLight.color, DesignedWindowLight);
            Assert.That(fill.intensity, Is.EqualTo(FillIntensity).Within(.001f));
            Assert.That(lamp.intensity, Is.EqualTo(LampIntensity).Within(.001f));
            AssertColor(window.sharedMaterial.color, DesignedWindow);
            AssertColor(RenderSettings.ambientSkyColor, DesignedAmbientSky);
        }

        [UnityTest]
        public IEnumerator Sunset_IsBetweenDayAndNight()
        {
            interior = NewInterior();
            yield return null;

            interior.ShowTime(Sunset);

            Assert.That(
                windowLight.intensity,
                Is.LessThan(WindowLightIntensity)
                    .And.GreaterThan(WindowLightIntensity * daylight.NightWindowLightScale)
            );
        }

        [UnityTest]
        public IEnumerator SkyboxAmbientRoom_DimsAmbientIntensity()
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            interior = NewInterior();
            yield return null;

            interior.ShowTime(Midnight);

            Assert.That(
                RenderSettings.ambientIntensity,
                Is.EqualTo(daylight.NightInteriorAmbientScale).Within(.001f)
            );
        }

        [UnityTest]
        public IEnumerator Night_LiftsUiGlassBackdropSoUiKeepsDaylightLook()
        {
            interior = NewInterior();
            yield return null;

            interior.ShowTime(Midnight);
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(1f).Within(.001f),
                "한밤에는 유리 UI가 비추는 뒤 화면을 낮 밝기까지 밝혀야 합니다."
            );

            interior.ShowTime(Sunset);
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(daylight.NightAmount(Sunset)).Within(.001f)
            );

            interior.ShowTime(Midday);
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(0f).Within(.001f),
                "낮에는 유리 UI를 디자인 그대로 둡니다."
            );
        }

        [UnityTest]
        public IEnumerator Destroy_PutsDesignedWindowsBack()
        {
            interior = NewInterior();
            yield return null;
            interior.ShowTime(Midnight);
            Assert.That(
                window.sharedMaterial,
                Is.Not.SameAs(designedWindow),
                "원본 대신 사본을 칠해야 합니다."
            );

            Object.Destroy(interior.gameObject);
            yield return null;

            Assert.That(window.sharedMaterial, Is.SameAs(designedWindow));
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(0f).Within(.001f),
                "장소를 떠나면 유리 UI를 디자인 그대로 돌려야 합니다."
            );
        }

        private InteriorDaylight NewInterior()
        {
            var root = new GameObject("InteriorDaylight");
            root.SetActive(false);
            var component = root.AddComponent<InteriorDaylight>();
            var fields = new SerializedObject(component);
            fields.FindProperty("daylight").objectReferenceValue = daylight;
            SetLights(fields.FindProperty("windowLights"), windowLight);
            SetLights(fields.FindProperty("fills"), fill);
            SetLights(fields.FindProperty("lamps"), lamp);
            fields.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return component;
        }

        private static void SetLights(SerializedProperty property, Light light)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = light;
        }

        private Light NewLight(string name, LightType type, float intensity)
        {
            var light = Keep(new GameObject(name)).AddComponent<Light>();
            light.type = type;
            light.intensity = intensity;
            return light;
        }

        private T Keep<T>(T item)
            where T : Object
        {
            created.Add(item);
            return item;
        }

        private static void AssertColor(Color actual, Color expected, string message = null)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.002f), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.002f), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.002f), message);
        }
    }
}
#endif
