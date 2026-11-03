#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
        private const float EffectHeightMeters = 7f;
        private const float ClearSunIntensity = 1.7f;
        private const float ClearShadowStrength = .78f;
        private const float ClearFogEndMeters = 125f;

        private static readonly Color ClearZenith = new Color(.23f, .65f, .95f);
        private static readonly Color ClearAmbientSky = new Color(.55f, .7f, .85f);
        private static readonly Color ClearCloud = new Color(1f, .98f, .95f);

        private static readonly WeatherLook CloudyLook = Look(
            .95f,
            .35f,
            new Color(.47f, .53f, .6f),
            110f
        );
        private static readonly WeatherLook RainLook = Look(
            .6f,
            .15f,
            new Color(.36f, .41f, .47f),
            80f
        );
        private static readonly WeatherLook SnowLook = Look(
            1.1f,
            .4f,
            new Color(.7f, .74f, .8f),
            70f
        );

        private readonly SavedRenderSettings saved = new SavedRenderSettings();
        private readonly List<Object> created = new List<Object>();
        private WeatherSettings settings;
        private Material designedSky;
        private Material designedCloud;
        private Light sun;
        private GameObject overcast;
        private Renderer cloud;
        private Transform player;
        private WeatherPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            saved.Read();
            settings = WeatherSettings.Create(
                TransitionSeconds,
                EffectHeightMeters,
                Keep(WeatherDefinition.Create("CLEAR", default, keepDesignedLook: true)),
                Keep(WeatherDefinition.Create("CLOUDY", CloudyLook, coversSky: true)),
                Keep(
                    WeatherDefinition.Create(
                        "RAIN",
                        RainLook,
                        coversSky: true,
                        effect: EffectTemplate("RainEffect")
                    )
                ),
                // 코드에 없는 새 날씨도 정의만 넣으면 보이는지 확인한다.
                Keep(
                    WeatherDefinition.Create(
                        "SNOW",
                        SnowLook,
                        coversSky: true,
                        effect: EffectTemplate("SnowEffect")
                    )
                )
            );
            Keep(settings);
            SetUpDesignedClearDay();
            presenter = NewPresenter();
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null)
                Object.DestroyImmediate(presenter.gameObject);
            foreach (Object item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            saved.Restore();
        }

        [UnityTest]
        public IEnumerator Rain_AfterTransition_DarkensSkyAndDropsRain()
        {
            presenter.Show("RAIN", "CLEAR");
            yield return WaitForTransition();

            Assert.That(presenter.Current.Code, Is.EqualTo("RAIN"));
            Assert.That(Effect("RainEffect").isEmitting, Is.True, "빗방울이 내려야 합니다.");
            Assert.That(overcast.activeSelf, Is.True, "비구름이 하늘을 덮어야 합니다.");
            Assert.That(sun.intensity, Is.EqualTo(RainLook.sunIntensity).Within(.001f));
            AssertColor(RenderSettings.ambientSkyColor, RainLook.ambientSky);
            AssertColor(RenderSettings.fogColor, RainLook.fogColor);
            Assert.That(
                RenderSettings.fogEndDistance,
                Is.EqualTo(RainLook.fogEndMeters).Within(.01f)
            );
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), RainLook.skyZenith);
            AssertColor(cloud.sharedMaterial.color, RainLook.cloudColor);
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
            presenter.Show("CLOUDY", "CLEAR");
            yield return WaitForTransition();

            Assert.That(presenter.Current.Code, Is.EqualTo("CLOUDY"));
            Assert.That(overcast.activeSelf, Is.True, "구름이 하늘을 덮어야 합니다.");
            Assert.That(
                presenter.GetComponentInChildren<ParticleSystem>(),
                Is.Null,
                "흐린 날에는 비가 오지 않습니다."
            );
            Assert.That(sun.intensity, Is.EqualTo(CloudyLook.sunIntensity).Within(.001f));
            Assert.That(sun.shadowStrength, Is.LessThan(ClearShadowStrength));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), CloudyLook.skyZenith);
            AssertColor(cloud.sharedMaterial.color, CloudyLook.cloudColor);
        }

        [UnityTest]
        public IEnumerator Clear_AfterRain_RestoresDesignedSceneAndPutsRainAway()
        {
            presenter.Show("RAIN", "CLEAR");
            yield return WaitForTransition();
            ParticleSystem rain = Effect("RainEffect");
            Assert.That(rain.isEmitting, Is.True, "맑아지기 전에는 비가 오고 있어야 합니다.");

            presenter.Show("CLEAR", "CLEAR");
            yield return WaitForTransition();

            Assert.That(rain.gameObject.activeSelf, Is.False, "맑아지면 빗방울을 거둬야 합니다.");
            Assert.That(overcast.activeSelf, Is.False, "맑아지면 덮던 구름을 걷어야 합니다.");
            Assert.That(sun.intensity, Is.EqualTo(ClearSunIntensity).Within(.001f));
            Assert.That(sun.shadowStrength, Is.EqualTo(ClearShadowStrength).Within(.001f));
            AssertColor(RenderSettings.ambientSkyColor, ClearAmbientSky);
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(ClearFogEndMeters).Within(.01f));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), ClearZenith);
            AssertColor(cloud.sharedMaterial.color, ClearCloud);
        }

        [UnityTest]
        public IEnumerator NewWeatherAsset_ShowsItsOwnLookAndEffect()
        {
            presenter.Show("SNOW", "CLEAR");
            yield return WaitForTransition();

            Assert.That(presenter.Current.Code, Is.EqualTo("SNOW"));
            Assert.That(Effect("SnowEffect").isEmitting, Is.True, "눈송이가 내려야 합니다.");
            Assert.That(
                presenter.transform.Find("RainEffect"),
                Is.Null,
                "다른 날씨 효과는 만들지 않습니다."
            );
            Assert.That(sun.intensity, Is.EqualTo(SnowLook.sunIntensity).Within(.001f));
        }

        [UnityTest]
        public IEnumerator UnknownWeather_ShowsFallbackWeather()
        {
            presenter.Show("HAIL", "RAIN");
            yield return WaitForTransition();

            Assert.That(presenter.Current.Code, Is.EqualTo("RAIN"));
            Assert.That(Effect("RainEffect").isEmitting, Is.True);
        }

        [UnityTest]
        public IEnumerator Show_ChangesSkyGraduallyInsteadOfAtOnce()
        {
            Object.DestroyImmediate(presenter.gameObject);
            settings = Keep(
                WeatherSettings.Create(
                    5f,
                    EffectHeightMeters,
                    Keep(WeatherDefinition.Create("RAIN", RainLook, coversSky: true))
                )
            );
            presenter = NewPresenter();

            presenter.Show("RAIN", "CLEAR");
            yield return null;

            Assert.That(
                sun.intensity,
                Is.LessThan(ClearSunIntensity).And.GreaterThan(RainLook.sunIntensity)
            );
        }

        [UnityTest]
        public IEnumerator Effect_FollowsPlayerFromAbove()
        {
            presenter.Show("RAIN", "CLEAR");
            player.position = new Vector3(10f, 0f, -4f);
            yield return null;

            Vector3 expected = player.position + Vector3.up * EffectHeightMeters;
            Assert.That(
                Vector3.Distance(Effect("RainEffect").transform.position, expected),
                Is.LessThan(.001f)
            );
        }

        [Test]
        public void ShowImmediately_AppliesWeatherWithoutWaitingForFrames()
        {
            presenter.ShowImmediately("RAIN");

            Assert.That(Effect("RainEffect").isEmitting, Is.True);
            Assert.That(overcast.activeSelf, Is.True);
            Assert.That(sun.intensity, Is.EqualTo(RainLook.sunIntensity).Within(.001f));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), RainLook.skyZenith);
        }

        [UnityTest]
        public IEnumerator Destroy_PutsDesignedSkyAndCloudsBack()
        {
            presenter.Show("RAIN", "CLEAR");
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

        private WeatherPresenter NewPresenter()
        {
            var root = new GameObject("TownWeather");
            root.SetActive(false);
            var component = root.AddComponent<WeatherPresenter>();
            var fields = new SerializedObject(component);
            fields.FindProperty("settings").objectReferenceValue = settings;
            fields.FindProperty("sun").objectReferenceValue = sun;
            fields.FindProperty("clouds").objectReferenceValue = cloud.transform.parent;
            fields.FindProperty("overcastClouds").objectReferenceValue = overcast;
            fields.FindProperty("effectFollowTarget").objectReferenceValue = player;
            fields.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return component;
        }

        private ParticleSystem Effect(string name)
        {
            Transform effect = presenter.transform.Find(name);
            Assert.That(effect, Is.Not.Null, name + " 효과가 없습니다.");
            return effect.GetComponent<ParticleSystem>();
        }

        private GameObject EffectTemplate(string name)
        {
            // 프리팹 대신 꺼 둔 객체를 복제 원본으로 쓴다.
            var template = new GameObject(name);
            template.SetActive(false);
            var particles = template.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            Keep(template);
            return template;
        }

        private void SetUpDesignedClearDay()
        {
            designedSky = Keep(new Material(Shader.Find("Elsehere/SummerSky")));
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
            Keep(sun.gameObject);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .93f, .81f);
            sun.intensity = ClearSunIntensity;
            sun.shadowStrength = ClearShadowStrength;

            designedCloud = Keep(
                new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = ClearCloud }
            );
            Transform clouds = Keep(new GameObject("SummerClouds")).transform;
            cloud = Cloud(clouds);
            overcast = Keep(new GameObject("OvercastClouds"));
            Cloud(overcast.transform);
            overcast.SetActive(false);
            player = Keep(new GameObject("Player")).transform;
        }

        private Renderer Cloud(Transform parent)
        {
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shape.transform.SetParent(parent, false);
            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = designedCloud;
            return renderer;
        }

        private T Keep<T>(T item)
            where T : Object
        {
            created.Add(item);
            return item;
        }

        private static WeatherLook Look(
            float sunIntensity,
            float shadowStrength,
            Color sky,
            float fogEndMeters
        ) =>
            new WeatherLook
            {
                skyZenith = sky,
                skyHorizon = sky,
                sunColor = Color.white,
                sunIntensity = sunIntensity,
                sunShadowStrength = shadowStrength,
                ambientSky = sky,
                ambientEquator = sky,
                ambientGround = sky * .5f,
                fogColor = sky,
                fogStartMeters = 20f,
                fogEndMeters = fogEndMeters,
                cloudColor = sky,
            };

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
    }
}
#endif
