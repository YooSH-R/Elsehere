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
    public class TownDaylightTests
    {
        private const double Midnight = 0;
        private const double Midday = 12 * 60 + 30;

        // 해 지기 15분 전. 노을은 해 지는 19시를 가운데로 앞뒤 30분 동안 물든다.
        private const double Sunset = 18 * 60 + 45;
        private const float LampIntensity = 4f;
        private const float LampCoreStrength = 1.2f;
        private const float TransitionSeconds = .2f;

        private static readonly Quaternion DesignedSun = Quaternion.Euler(48f, -35f, 0f);
        private static readonly Color GlowEmission = new Color(.65f, .525f, .3f);
        private static readonly WeatherLook Designed = new WeatherLook
        {
            skyZenith = new Color(.23f, .65f, .95f),
            skyHorizon = new Color(.72f, .88f, .94f),
            sunColor = new Color(1f, .93f, .81f),
            sunIntensity = 1.7f,
            sunShadowStrength = .78f,
            ambientSky = new Color(.55f, .7f, .85f),
            ambientEquator = new Color(.7f, .64f, .53f),
            ambientGround = new Color(.34f, .31f, .27f),
            fogColor = new Color(.65f, .82f, .87f),
            fogStartMeters = 42f,
            fogEndMeters = 125f,
            cloudColor = new Color(1f, .98f, .95f),
        };
        private static readonly WeatherLook RainLook = new WeatherLook
        {
            skyZenith = new Color(.36f, .41f, .47f),
            skyHorizon = new Color(.58f, .62f, .66f),
            sunColor = new Color(.82f, .86f, .92f),
            sunIntensity = .6f,
            sunShadowStrength = .15f,
            ambientSky = new Color(.45f, .5f, .56f),
            ambientEquator = new Color(.45f, .46f, .47f),
            ambientGround = new Color(.25f, .25f, .26f),
            fogColor = new Color(.53f, .57f, .62f),
            fogStartMeters = 18f,
            fogEndMeters = 80f,
            cloudColor = new Color(.6f, .63f, .67f),
        };

        private readonly SavedRenderSettings saved = new SavedRenderSettings();
        private readonly List<Object> created = new List<Object>();
        private DaylightSettings daylight;
        private WeatherSettings weathers;
        private Material designedSky;
        private Material designedGlow;
        private Material designedLampGlass;
        private Light sun;
        private Light lamp;
        private Renderer glow;
        private Renderer lampGlass;
        private WeatherPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            saved.Read();
            designedGlow = Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            designedGlow.EnableKeyword("_EMISSION");
            designedGlow.SetColor("_EmissionColor", GlowEmission);
            designedLampGlass = Keep(new Material(Shader.Find("Elsehere/WarmLamp")));
            designedLampGlass.SetFloat("_CoreStrength", LampCoreStrength);
            daylight = Keep(DaylightSettings.Create(new[] { designedGlow, designedLampGlass }));
            weathers = Keep(
                WeatherSettings.Create(
                    TransitionSeconds,
                    7f,
                    Keep(WeatherDefinition.Create("CLEAR", default, keepDesignedLook: true)),
                    Keep(
                        WeatherDefinition.Create(
                            "RAIN",
                            RainLook,
                            coversSky: true,
                            effect: RainTemplate()
                        )
                    )
                )
            );
            SetUpDesignedTown();
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
        public IEnumerator Midnight_DarkensSkySunAndAmbientUnderStars()
        {
            yield return null;
            WeatherLook expected = daylight.Mix(Designed, Midnight, skyCovered: false);

            presenter.ShowTime(Midnight);

            Assert.That(sun.intensity, Is.EqualTo(expected.sunIntensity).Within(.001f));
            Assert.That(sun.intensity, Is.LessThan(Designed.sunIntensity));
            AssertColor(RenderSettings.ambientSkyColor, expected.ambientSky);
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), expected.skyZenith);
            Assert.That(
                RenderSettings.skybox.GetFloat("_Stars"),
                Is.EqualTo(1f).Within(.001f),
                "맑은 밤에는 별이 떠야 합니다."
            );
            Assert.That(
                Quaternion.Angle(sun.transform.rotation, DesignedSun),
                Is.LessThan(1f),
                "달빛은 디자인한 방향에서 듭니다."
            );
        }

        [UnityTest]
        public IEnumerator Midnight_BrightensStreetLampsAndGlowingWindows()
        {
            yield return null;

            presenter.ShowTime(Midnight);

            Assert.That(
                lamp.intensity,
                Is.EqualTo(LampIntensity * daylight.NightLampScale).Within(.001f)
            );
            Assert.That(
                glow.sharedMaterial.GetColor("_EmissionColor").r,
                Is.EqualTo(GlowEmission.r * daylight.NightGlowScale).Within(.002f)
            );
            Assert.That(
                lampGlass.sharedMaterial.GetFloat("_CoreStrength"),
                Is.EqualTo(LampCoreStrength * daylight.NightGlowScale).Within(.002f)
            );
            Assert.That(
                designedGlow.GetColor("_EmissionColor").r,
                Is.EqualTo(GlowEmission.r).Within(.002f),
                "원본 재질은 바뀌면 안 됩니다."
            );
        }

        [UnityTest]
        public IEnumerator Midday_KeepsDesignedTown()
        {
            yield return null;
            presenter.ShowTime(Midnight);

            presenter.ShowTime(Midday);

            Assert.That(sun.intensity, Is.EqualTo(Designed.sunIntensity).Within(.001f));
            Assert.That(lamp.intensity, Is.EqualTo(LampIntensity).Within(.001f));
            Assert.That(RenderSettings.skybox.GetFloat("_Stars"), Is.EqualTo(0f).Within(.001f));
            Assert.That(Quaternion.Angle(sun.transform.rotation, DesignedSun), Is.LessThan(1f));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), Designed.skyZenith);
        }

        [UnityTest]
        public IEnumerator Sunset_LowersSunAndWarmsHorizon()
        {
            yield return null;

            presenter.ShowTime(Sunset);

            float elevation =
                Mathf.Asin(-(sun.transform.rotation * Vector3.forward).y) * Mathf.Rad2Deg;
            Color horizon = RenderSettings.skybox.GetColor("_Horizon");
            Assert.That(elevation, Is.LessThan(20f), "해가 지평선 가까이 낮아져야 합니다.");
            Assert.That(
                horizon.r - horizon.b,
                Is.GreaterThan(Designed.skyHorizon.r - Designed.skyHorizon.b)
            );
        }

        [UnityTest]
        public IEnumerator RainyNight_StaysDarkWithRainAndNoStars()
        {
            yield return null;
            WeatherLook expected = daylight.Mix(RainLook, Midnight, skyCovered: true);

            presenter.Show("RAIN", "CLEAR");
            presenter.ShowTime(Midnight);
            yield return new WaitForSeconds(TransitionSeconds + .1f);
            yield return null;

            Assert.That(sun.intensity, Is.EqualTo(expected.sunIntensity).Within(.001f));
            Assert.That(
                RenderSettings.skybox.GetFloat("_Stars"),
                Is.EqualTo(0f).Within(.001f),
                "구름이 덮은 밤에는 별이 보이지 않습니다."
            );
            Assert.That(
                presenter.transform.Find("RainEffect").GetComponent<ParticleSystem>().isEmitting,
                Is.True
            );
        }

        [UnityTest]
        public IEnumerator Destroy_PutsDesignedGlowBack()
        {
            yield return null;
            presenter.ShowTime(Midnight);
            Assert.That(
                glow.sharedMaterial,
                Is.Not.SameAs(designedGlow),
                "원본 대신 사본을 밝혀야 합니다."
            );

            Object.Destroy(presenter.gameObject);
            yield return null;

            Assert.That(glow.sharedMaterial, Is.SameAs(designedGlow));
            Assert.That(lampGlass.sharedMaterial, Is.SameAs(designedLampGlass));
        }

        private WeatherPresenter NewPresenter()
        {
            var root = new GameObject("TownWeather");
            root.SetActive(false);
            var component = root.AddComponent<WeatherPresenter>();
            var clouds = Keep(new GameObject("SummerClouds"));
            var overcast = Keep(new GameObject("OvercastClouds"));
            var player = Keep(new GameObject("Player"));
            var fields = new SerializedObject(component);
            fields.FindProperty("settings").objectReferenceValue = weathers;
            fields.FindProperty("sun").objectReferenceValue = sun;
            fields.FindProperty("clouds").objectReferenceValue = clouds.transform;
            fields.FindProperty("overcastClouds").objectReferenceValue = overcast;
            fields.FindProperty("effectFollowTarget").objectReferenceValue = player.transform;
            fields.FindProperty("daylight").objectReferenceValue = daylight;
            SerializedProperty lamps = fields.FindProperty("lamps");
            lamps.arraySize = 1;
            lamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            fields.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return component;
        }

        private void SetUpDesignedTown()
        {
            designedSky = Keep(new Material(Shader.Find("Elsehere/SummerSky")));
            designedSky.SetColor("_Zenith", Designed.skyZenith);
            designedSky.SetColor("_Horizon", Designed.skyHorizon);
            RenderSettings.skybox = designedSky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Designed.ambientSky;
            RenderSettings.ambientEquatorColor = Designed.ambientEquator;
            RenderSettings.ambientGroundColor = Designed.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Designed.fogColor;
            RenderSettings.fogStartDistance = Designed.fogStartMeters;
            RenderSettings.fogEndDistance = Designed.fogEndMeters;

            sun = Keep(new GameObject("Sun")).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Designed.sunColor;
            sun.intensity = Designed.sunIntensity;
            sun.shadowStrength = Designed.sunShadowStrength;
            sun.transform.rotation = DesignedSun;

            lamp = Keep(new GameObject("LanternLight")).AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = LampIntensity;

            glow = Shape("WindowAmberGlow", designedGlow);
            lampGlass = Shape("LanternAmberV2", designedLampGlass);
        }

        private Renderer Shape(string name, Material material)
        {
            GameObject shape = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
            shape.name = name;
            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private GameObject RainTemplate()
        {
            var template = Keep(new GameObject("RainEffect"));
            template.SetActive(false);
            ParticleSystem.MainModule main = template.AddComponent<ParticleSystem>().main;
            main.playOnAwake = false;
            return template;
        }

        private T Keep<T>(T item)
            where T : Object
        {
            created.Add(item);
            return item;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.002f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.002f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.002f));
        }
    }
}
#endif
