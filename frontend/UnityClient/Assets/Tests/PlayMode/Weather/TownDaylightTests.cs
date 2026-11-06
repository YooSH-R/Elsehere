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
    public class TownDaylightTests
    {
        private const double Midnight = 0;
        private const double Midday = 12 * 60 + 30;

        // 해 지기 15분 전. 노을은 해 지는 19시를 가운데로 앞뒤 30분 동안 물든다.
        private const double Sunset = 18 * 60 + 45;
        private const float LampIntensity = 4f;
        private const float FixtureIntensity = .1f;
        private const float ShopLampIntensity = .9f;
        private const float LampCoreStrength = 1.2f;
        private const float TransitionSeconds = .2f;

        private static readonly Quaternion DesignedSun = Quaternion.Euler(48f, -35f, 0f);
        private static readonly Color GlowEmission = new Color(.65f, .525f, .3f);
        private static readonly Color DesignedWindowGlass = new Color(.451f, .624f, .686f);
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
        private Material designedWindowGlass;
        private Light sun;
        private Light lamp;
        private Light fixture;
        private Light shopLamp;
        private Renderer glow;
        private Renderer lampGlass;
        private Transform shop;
        private Renderer shopWindow;
        private Renderer houseWindow;
        private WeatherPresenter presenter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return IsolatedScene.Open();
            saved.Read();
            designedGlow = Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            designedGlow.EnableKeyword("_EMISSION");
            designedGlow.SetColor("_EmissionColor", GlowEmission);
            designedLampGlass = Keep(new Material(Shader.Find("Elsehere/WarmLamp")));
            designedLampGlass.SetFloat("_CoreStrength", LampCoreStrength);
            designedWindowGlass = Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            designedWindowGlass.color = DesignedWindowGlass;
            daylight = Keep(
                DaylightSettings.Create(
                    new[] { designedGlow },
                    litWindowMaterials: new[] { designedWindowGlass },
                    nightOnlyGlowMaterials: new[] { designedLampGlass }
                )
            );
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
        public IEnumerator Midnight_LightsShopRoomsBrighterThanShopWallLamps()
        {
            yield return null;
            // 상점 외벽 등은 입점 건물에 붙어 있어도 방을 밝히는 실내등이 아니다.
            fixture.transform.SetParent(shop);

            presenter.ShowTime(Midnight);

            Assert.That(
                daylight.NightShopLampScale,
                Is.GreaterThan(daylight.NightLampScale),
                "마을 안 매장은 밤 환경광이 어두워져도 불을 켠 방처럼 밝아야 합니다."
            );
            Assert.That(
                shopLamp.intensity,
                Is.EqualTo(ShopLampIntensity * daylight.NightShopLampScale).Within(.001f)
            );
            Assert.That(
                fixture.intensity,
                Is.EqualTo(FixtureIntensity * daylight.NightLampScale).Within(.001f),
                "상점 벽등은 그대로 밤 불빛 배율을 씁니다."
            );

            presenter.ShowTime(Midday);

            Assert.That(shopLamp.intensity, Is.EqualTo(ShopLampIntensity).Within(.001f));
        }

        [UnityTest]
        public IEnumerator Midnight_LightsLoadedShopInteriorLikeALitRoom()
        {
            yield return null;
            // 방문한 건물의 실내는 따로 불러와 마을 날씨 표시에 넘긴다.
            Transform interior = Keep(new GameObject("PoliceStationInterior")).transform;
            Light ceiling = Keep(new GameObject("InteriorLight")).AddComponent<Light>();
            ceiling.type = LightType.Point;
            ceiling.intensity = ShopLampIntensity;
            ceiling.transform.SetParent(interior);
            presenter.SetShopInterior(interior);

            presenter.ShowTime(Midnight);

            Assert.That(
                ceiling.intensity,
                Is.EqualTo(ShopLampIntensity * daylight.NightShopLampScale).Within(.001f)
            );

            presenter.ShowTime(Midday);

            Assert.That(ceiling.intensity, Is.EqualTo(ShopLampIntensity).Within(.001f));
        }

        // 카페·호텔·드럭스토어 실내 씬처럼, 매장 안에 있는 동안은 밤에도 불을 켠 방의 환경광이다.
        // 마을 하늘의 푸른 환경광이 아니라 실내 씬과 같은 밝은 실내 환경광을 쓴다.
        [UnityTest]
        public IEnumerator Midnight_InsideLoadedShop_KeepsRoomAmbientLikeInteriorScenes()
        {
            yield return null;
            Transform interior = Keep(new GameObject("PoliceStationInterior")).transform;
            presenter.ShowTime(Midnight);

            presenter.SetShopInterior(interior);

            AssertColor(
                RenderSettings.ambientSkyColor,
                daylight.ShopInteriorAmbientSky * daylight.NightInteriorAmbientScale
            );
            AssertColor(
                RenderSettings.ambientGroundColor,
                daylight.ShopInteriorAmbientGround * daylight.NightInteriorAmbientScale
            );

            presenter.ShowTime(Midday);

            AssertColor(RenderSettings.ambientSkyColor, daylight.ShopInteriorAmbientSky);
            AssertColor(RenderSettings.ambientEquatorColor, daylight.ShopInteriorAmbientEquator);
        }

        // 매장 안은 실내라 비 오는 날에도 마을의 어두운 날씨 환경광을 받지 않는다.
        [UnityTest]
        public IEnumerator RainyDay_InsideLoadedShop_KeepsRoomAmbient()
        {
            yield return null;
            presenter.Show("RAIN", "CLEAR");
            presenter.ShowTime(Midday);
            yield return new WaitForSeconds(TransitionSeconds + .1f);

            presenter.SetShopInterior(Keep(new GameObject("BakeryInterior")).transform);

            AssertColor(RenderSettings.ambientSkyColor, daylight.ShopInteriorAmbientSky);
        }

        // 마을 해가 지붕·벽 그림자로 매장 안을 뒤덮지 않도록, 안에 있는 동안만 그림자를 옅게 한다.
        [UnityTest]
        public IEnumerator Midday_InsideLoadedShop_SoftensSunShadows()
        {
            yield return null;
            presenter.ShowTime(Midday);

            presenter.SetShopInterior(Keep(new GameObject("PoliceStationInterior")).transform);

            Assert.That(
                sun.shadowStrength,
                Is.EqualTo(daylight.ShopInteriorSunShadowStrength).Within(.001f)
            );

            presenter.SetShopInterior(null);

            Assert.That(sun.shadowStrength, Is.EqualTo(Designed.sunShadowStrength).Within(.001f));
        }

        [UnityTest]
        public IEnumerator Midnight_LeavingLoadedShop_PutsTownNightAmbientBack()
        {
            yield return null;
            WeatherLook expected = daylight.Mix(Designed, Midnight, skyCovered: false);
            presenter.ShowTime(Midnight);
            presenter.SetShopInterior(Keep(new GameObject("PoliceStationInterior")).transform);

            presenter.SetShopInterior(null);

            AssertColor(RenderSettings.ambientSkyColor, expected.ambientSky);
        }

        [UnityTest]
        public IEnumerator Midnight_LightsWindowsOfOpenShopsOnly()
        {
            yield return null;

            presenter.ShowTime(Midnight);

            AssertColor(shopWindow.sharedMaterial.color, daylight.LitWindowColor);
            AssertColor(
                shopWindow.sharedMaterial.GetColor("_EmissionColor"),
                daylight.LitWindowEmission
            );
            Assert.That(
                houseWindow.sharedMaterial,
                Is.SameAs(designedWindowGlass),
                "입점하지 않은 주택 창은 그대로여야 합니다."
            );
            AssertColor(designedWindowGlass.color, DesignedWindowGlass);

            presenter.ShowTime(Midday);

            AssertColor(shopWindow.sharedMaterial.color, DesignedWindowGlass);
        }

        // 가로등이 아닌 등(상점 벽등·실내등)과 창빛은 낮에 씬 디자인 그대로다.
        [UnityTest]
        public IEnumerator Midday_KeepsDesignedTown()
        {
            yield return null;
            presenter.ShowTime(Midnight);

            presenter.ShowTime(Midday);

            Assert.That(sun.intensity, Is.EqualTo(Designed.sunIntensity).Within(.001f));
            Assert.That(fixture.intensity, Is.EqualTo(FixtureIntensity).Within(.001f));
            Assert.That(
                glow.sharedMaterial.GetColor("_EmissionColor").r,
                Is.EqualTo(GlowEmission.r).Within(.002f)
            );
            Assert.That(RenderSettings.skybox.GetFloat("_Stars"), Is.EqualTo(0f).Within(.001f));
            Assert.That(Quaternion.Angle(sun.transform.rotation, DesignedSun), Is.LessThan(1f));
            AssertColor(RenderSettings.skybox.GetColor("_Zenith"), Designed.skyZenith);
        }

        // 가로등은 밤에만 켠다. 낮에는 빛과 전구를 끈다.
        [UnityTest]
        public IEnumerator Midday_TurnsStreetLampsOff()
        {
            yield return null;
            presenter.ShowTime(Midnight);

            presenter.ShowTime(Midday);

            Assert.That(lamp.intensity, Is.EqualTo(0f).Within(.001f));
            Assert.That(lamp.enabled, Is.False, "꺼진 가로등 빛은 그리지 않습니다.");
            Assert.That(
                lampGlass.sharedMaterial.GetFloat("_CoreStrength"),
                Is.EqualTo(0f).Within(.002f)
            );
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
        public IEnumerator Night_LiftsUiGlassBackdropSoUiKeepsDaylightLook()
        {
            yield return null;

            presenter.ShowTime(Midnight);
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(1f).Within(.001f),
                "한밤에는 유리 UI가 비추는 뒤 화면을 낮 밝기까지 밝혀야 합니다."
            );

            presenter.ShowTime(Sunset);
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(daylight.NightAmount(Sunset)).Within(.001f)
            );

            presenter.ShowTime(Midday);
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(0f).Within(.001f),
                "낮에는 유리 UI를 디자인 그대로 둡니다."
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
            Assert.That(shopWindow.sharedMaterial, Is.SameAs(designedWindowGlass));
            Assert.That(
                UiBackdropLift.Amount,
                Is.EqualTo(0f).Within(.001f),
                "장소를 떠나면 유리 UI를 디자인 그대로 돌려야 합니다."
            );
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
            lamps.arraySize = 2;
            lamps.GetArrayElementAtIndex(0).objectReferenceValue = fixture;
            lamps.GetArrayElementAtIndex(1).objectReferenceValue = shopLamp;
            SerializedProperty streetLamps = fields.FindProperty("streetLamps");
            streetLamps.arraySize = 1;
            streetLamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            SerializedProperty litBuildings = fields.FindProperty("litBuildings");
            litBuildings.arraySize = 1;
            litBuildings.GetArrayElementAtIndex(0).objectReferenceValue = shop;
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
            fixture = Keep(new GameObject("FixtureLight")).AddComponent<Light>();
            fixture.type = LightType.Point;
            fixture.intensity = FixtureIntensity;

            glow = Shape("WindowAmberGlow", designedGlow);
            lampGlass = Shape("LanternAmberV2", designedLampGlass);

            // 입점한 매장과 주택이 같은 창 유리 재질을 쓴다.
            shop = Keep(new GameObject("MapleDiner")).transform;
            shopWindow = Shape("WindowBlue", designedWindowGlass);
            shopWindow.transform.SetParent(shop);
            houseWindow = Shape("WindowBlue", designedWindowGlass);
            houseWindow.transform.SetParent(Keep(new GameObject("BrickResidence2F")).transform);
            // 매장 실내(Interior)의 천장 등. 마을 밤 등 목록에 가로등과 함께 들어 있다.
            shopLamp = Keep(new GameObject("InteriorLight")).AddComponent<Light>();
            shopLamp.type = LightType.Point;
            shopLamp.intensity = ShopLampIntensity;
            shopLamp.transform.SetParent(shop);
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
