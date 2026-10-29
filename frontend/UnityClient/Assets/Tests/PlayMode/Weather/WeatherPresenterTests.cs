#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace A305.Weather.PlayModeTests
{
    public class WeatherPresenterTests
    {
        private const float TransitionSeconds = .2f;
        private const float ClearSunIntensity = 1.7f;
        private const float ClearShadowStrength = .78f;
        private const float ClearFogEndMeters = 125f;

        private static readonly Color ClearZenith = new Color(.23f, .65f, .95f);
        private static readonly Color ClearAmbientSky = new Color(.55f, .7f, .85f);
        private static readonly Color ClearCloud = new Color(1f, .98f, .95f);

        private readonly SavedRenderSettings saved = new SavedRenderSettings();
        private WeatherSettings settings;
        private Material designedSky;
        private Material designedCloud;
        private Light sun;
        private Transform clouds;
        private GameObject overcast;
        private Renderer cloud;
        private ParticleSystem rain;
        private Transform player;
        private WeatherPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            saved.Read();
            settings = ScriptableObject.CreateInstance<WeatherSettings>();
            SetTransitionSeconds(TransitionSeconds);
            SetUpDesignedClearDay();

            var root = new GameObject("TownWeather");
            root.SetActive(false);
            presenter = root.AddComponent<WeatherPresenter>();
            var fields = new SerializedObject(presenter);
            fields.FindProperty("settings").objectReferenceValue = settings;
            fields.FindProperty("sun").objectReferenceValue = sun;
            fields.FindProperty("clouds").objectReferenceValue = clouds;
            fields.FindProperty("overcastClouds").objectReferenceValue = overcast;
            fields.FindProperty("rain").objectReferenceValue = rain;
            fields.FindProperty("rainFollowTarget").objectReferenceValue = player;
            fields.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null)
                Object.DestroyImmediate(presenter.gameObject);
            foreach (
                Object created in new Object[]
                {
                    sun.gameObject,
                    clouds.gameObject,
                    overcast,
                    rain.gameObject,
                    player.gameObject,
                    designedSky,
                    designedCloud,
                    settings,
                }
            )
                Object.DestroyImmediate(created);
            saved.Restore();
        }

        [UnityTest]
        public IEnumerator Rain_AfterTransition_DarkensSkyAndSunAndStartsRain()
        {
            WeatherLook expected = settings.LookFor(WeatherKind.Rain, default);

            presenter.Show(WeatherKind.Rain);
            yield return WaitForTransition();

            Assert.That(presenter.Current, Is.EqualTo(WeatherKind.Rain));
            Assert.That(rain.isEmitting, Is.True, "빗방울이 내려야 합니다.");
            Assert.That(overcast.activeSelf, Is.True, "비구름이 하늘을 덮어야 합니다.");
            Assert.That(sun.intensity, Is.EqualTo(expected.sunIntensity).Within(.001f));
            Assert.That(sun.intensity, Is.LessThan(ClearSunIntensity));
            AssertColor(RenderSettings.ambientSkyColor, expected.ambientSky);
            AssertColor(RenderSettings.fogColor, expected.fogColor);
            Assert.That(
                RenderSettings.fogEndDistance,
                Is.EqualTo(expected.fogEndMeters).Within(.01f)
            );
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), expected.skyZenith);
            Assert.That(
                RenderSettings.skybox.GetColor("_Zenith").grayscale,
                Is.LessThan(ClearZenith.grayscale)
            );
            AssertColor(cloud.sharedMaterial.color, expected.cloudColor);
            AssertColor(
                designedSky.GetColor("_Zenith"),
                ClearZenith,
                "원본 하늘 에셋은 바뀌면 안 됩니다."
            );
            AssertColor(designedCloud.color, ClearCloud, "원본 구름 에셋은 바뀌면 안 됩니다.");
        }

        [UnityTest]
        public IEnumerator Cloudy_AfterTransition_DimsSkyUnderCloudsWithoutRain()
        {
            WeatherLook expected = settings.LookFor(WeatherKind.Cloudy, default);

            presenter.Show(WeatherKind.Cloudy);
            yield return WaitForTransition();

            Assert.That(presenter.Current, Is.EqualTo(WeatherKind.Cloudy));
            Assert.That(rain.isEmitting, Is.False, "흐린 날에는 비가 오지 않습니다.");
            Assert.That(overcast.activeSelf, Is.True, "구름이 하늘을 덮어야 합니다.");
            Assert.That(sun.intensity, Is.EqualTo(expected.sunIntensity).Within(.001f));
            Assert.That(sun.shadowStrength, Is.LessThan(ClearShadowStrength));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), expected.skyZenith);
            AssertColor(cloud.sharedMaterial.color, expected.cloudColor);
        }

        [UnityTest]
        public IEnumerator Clear_AfterRain_RestoresDesignedSceneAndStopsRain()
        {
            presenter.Show(WeatherKind.Rain);
            yield return WaitForTransition();
            Assert.That(rain.isEmitting, Is.True, "맑아지기 전에는 비가 오고 있어야 합니다.");

            presenter.Show(WeatherKind.Clear);
            yield return WaitForTransition();

            Assert.That(rain.isEmitting, Is.False);
            Assert.That(overcast.activeSelf, Is.False, "맑아지면 덮던 구름을 걷어야 합니다.");
            Assert.That(sun.intensity, Is.EqualTo(ClearSunIntensity).Within(.001f));
            Assert.That(sun.shadowStrength, Is.EqualTo(ClearShadowStrength).Within(.001f));
            AssertColor(RenderSettings.ambientSkyColor, ClearAmbientSky);
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(ClearFogEndMeters).Within(.01f));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), ClearZenith);
            AssertColor(cloud.sharedMaterial.color, ClearCloud);
        }

        [UnityTest]
        public IEnumerator Show_ChangesSkyGraduallyInsteadOfAtOnce()
        {
            SetTransitionSeconds(5f);
            float rainIntensity = settings.LookFor(WeatherKind.Rain, default).sunIntensity;

            presenter.Show(WeatherKind.Rain);
            yield return null;

            Assert.That(
                sun.intensity,
                Is.LessThan(ClearSunIntensity).And.GreaterThan(rainIntensity)
            );
        }

        [UnityTest]
        public IEnumerator Rain_FollowsPlayerFromAbove()
        {
            presenter.Show(WeatherKind.Rain);
            player.position = new Vector3(10f, 0f, -4f);
            yield return null;

            Vector3 expected = player.position + Vector3.up * settings.RainHeightMeters;
            Assert.That(Vector3.Distance(rain.transform.position, expected), Is.LessThan(.001f));
        }

        [Test]
        public void ShowImmediately_AppliesWeatherWithoutWaitingForFrames()
        {
            WeatherLook expected = settings.LookFor(WeatherKind.Rain, default);

            presenter.ShowImmediately(WeatherKind.Rain);

            Assert.That(rain.isEmitting, Is.True);
            Assert.That(overcast.activeSelf, Is.True);
            Assert.That(sun.intensity, Is.EqualTo(expected.sunIntensity).Within(.001f));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), expected.skyZenith);
        }

        [UnityTest]
        public IEnumerator Destroy_PutsDesignedSkyAndCloudsBack()
        {
            presenter.Show(WeatherKind.Rain);
            yield return null;
            Assert.That(
                RenderSettings.skybox,
                Is.Not.SameAs(designedSky),
                "원본 대신 사본을 물들여야 합니다."
            );

            Object.Destroy(presenter.gameObject);
            yield return null;

            Assert.That(RenderSettings.skybox, Is.SameAs(designedSky));
            Assert.That(cloud.sharedMaterial, Is.SameAs(designedCloud));
        }

        private void SetUpDesignedClearDay()
        {
            designedSky = new Material(Shader.Find("Elsehere/SummerSky"));
            designedSky.SetColor("_Zenith", ClearZenith);
            designedSky.SetColor("_Horizon", new Color(.72f, .88f, .94f));
            RenderSettings.skybox = designedSky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ClearAmbientSky;
            RenderSettings.ambientEquatorColor = new Color(.7f, .64f, .53f);
            RenderSettings.ambientGroundColor = new Color(.34f, .31f, .27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.65f, .82f, .87f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = ClearFogEndMeters;

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .93f, .81f);
            sun.intensity = ClearSunIntensity;
            sun.shadowStrength = ClearShadowStrength;

            designedCloud = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = ClearCloud,
            };
            clouds = new GameObject("SummerClouds").transform;
            cloud = Cloud(clouds);
            overcast = new GameObject("OvercastClouds");
            Cloud(overcast.transform);
            overcast.SetActive(false);

            rain = new GameObject("Rain").AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = rain.main;
            main.playOnAwake = false;
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            player = new GameObject("Player").transform;
        }

        private Renderer Cloud(Transform parent)
        {
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shape.transform.SetParent(parent, false);
            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = designedCloud;
            return renderer;
        }

        private void SetTransitionSeconds(float seconds)
        {
            var fields = new SerializedObject(settings);
            fields.FindProperty("transitionSeconds").floatValue = seconds;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator WaitForTransition()
        {
            yield return new WaitForSeconds(TransitionSeconds + .1f);
            yield return null;
        }

        private static void AssertColor(Color actual, Color expected, string message = null)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.002f), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.002f), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.002f), message);
        }

        private sealed class SavedRenderSettings
        {
            private Material skybox;
            private AmbientMode ambientMode;
            private Color ambientSky;
            private Color ambientEquator;
            private Color ambientGround;
            private bool fog;
            private FogMode fogMode;
            private Color fogColor;
            private float fogStart;
            private float fogEnd;

            public void Read()
            {
                skybox = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                ambientSky = RenderSettings.ambientSkyColor;
                ambientEquator = RenderSettings.ambientEquatorColor;
                ambientGround = RenderSettings.ambientGroundColor;
                fog = RenderSettings.fog;
                fogMode = RenderSettings.fogMode;
                fogColor = RenderSettings.fogColor;
                fogStart = RenderSettings.fogStartDistance;
                fogEnd = RenderSettings.fogEndDistance;
            }

            public void Restore()
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
            }
        }
    }
}
#endif
