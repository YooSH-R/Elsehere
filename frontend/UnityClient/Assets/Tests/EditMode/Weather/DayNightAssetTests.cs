using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Weather.Tests
{
    /// <summary>마을과 실내에 실제로 쓰는 밤낮 에셋이 알맞은지 확인한다.</summary>
    public class DayNightAssetTests
    {
        private const string SettingsPath = "Assets/Data/Weather/DayNight.asset";

        private DaylightSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<DaylightSettings>(SettingsPath);
            Assert.That(settings, Is.Not.Null, SettingsPath + "가 없습니다.");
        }

        [Test]
        public void Midnight_IsDarkButNotBlack()
        {
            WeatherLook night = settings.Mix(ClearDay(), 0, skyCovered: false);

            Assert.That(night.sunIntensity, Is.LessThan(ClearDay().sunIntensity * .3f));
            Assert.That(night.sunIntensity, Is.GreaterThan(0f), "달빛으로 길이 보여야 합니다.");
            Assert.That(night.ambientSky.grayscale, Is.GreaterThan(.08f));
            Assert.That(
                night.ambientSky.grayscale,
                Is.LessThan(ClearDay().ambientSky.grayscale * .5f)
            );
        }

        [Test]
        public void Night_BrightensLampsAndGlowAndSoftensDaylightIndoors()
        {
            Assert.That(settings.NightLampScale, Is.GreaterThan(1f));
            Assert.That(settings.NightGlowScale, Is.GreaterThan(1f));
            Assert.That(settings.NightWindowLightScale, Is.LessThan(1f));
            Assert.That(settings.NightFillScale, Is.LessThan(1f));
            Assert.That(settings.NightInteriorAmbientScale, Is.LessThan(1f));
        }

        [Test]
        public void Night_KeepsRoomsLitAsIfLightsAreOn()
        {
            // 밤의 실내는 불을 켠 방이다. 간접등만 켠 것처럼 어두우면 안 된다.
            // 상점처럼 창으로 드는 빛 하나가 방 전체를 밝히는 곳도 있어 그 빛도 크게 줄이지 않는다.
            Assert.That(settings.NightWindowLightScale, Is.GreaterThanOrEqualTo(.7f));
            Assert.That(settings.NightInteriorAmbientScale, Is.GreaterThanOrEqualTo(.85f));
            Assert.That(settings.NightFillScale, Is.GreaterThanOrEqualTo(.8f));
            Assert.That(
                settings.NightRoomLightColor.r,
                Is.GreaterThan(settings.NightRoomLightColor.b),
                "밤 방 조명은 따뜻한 실내등 색이어야 합니다."
            );
        }

        [TestCase("WindowBlue")]
        [TestCase("GlassLight")]
        [TestCase("SkyGlassV2")]
        [TestCase("Glass")]
        public void OpenShopWindowsLightUpAtNight(string material)
        {
            Assert.That(
                settings.LitWindowMaterials.Select(item => item.name),
                Has.Member(material)
            );
            Assert.That(
                settings.LitWindowEmission.maxColorComponent,
                Is.GreaterThan(0f),
                "불 켠 창은 빛을 내야 합니다."
            );
            Assert.That(
                settings.LitWindowColor.r,
                Is.GreaterThan(settings.LitWindowColor.b),
                "실내등 빛은 따뜻한 색이어야 합니다."
            );
        }

        [TestCase("WindowAmberGlow")]
        [TestCase("Glow")]
        [TestCase("PendantLight")]
        public void Town_GlowMaterialsShineBrighterAtNight(string material)
        {
            Assert.That(settings.GlowMaterials.Select(item => item.name), Has.Member(material));
        }

        // 가로등 전구는 낮에 끄고 밤에만 밝힌다.
        [Test]
        public void Town_StreetLampBulbsShineOnlyAtNight()
        {
            Assert.That(
                settings.NightOnlyGlowMaterials.Select(item => item.name),
                Has.Member("LanternAmberV2")
            );
            Assert.That(
                settings.GlowMaterials.Select(item => item.name),
                Has.No.Member("LanternAmberV2")
            );
        }

        [TestCase("WindowDaylight")]
        [TestCase("DaylightGlass")]
        [TestCase("DrugstoreDaylightGlass")]
        public void Interior_WindowsTurnToNightSky(string material)
        {
            Material window = settings.WindowMaterials.SingleOrDefault(item =>
                item.name == material
            );

            Assert.That(window, Is.Not.Null, material + "가 창밖 재질 목록에 없습니다.");
            Assert.That(settings.NightWindowColor.grayscale, Is.LessThan(window.color.grayscale));
        }

        // MainstreetAtmosphere가 만드는 마을의 맑은 여름 하늘과 같은 값.
        private static WeatherLook ClearDay() =>
            new WeatherLook
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
    }
}
