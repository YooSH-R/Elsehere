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
        public void Night_BrightensLampsAndGlowAndDimsDaylightIndoors()
        {
            Assert.That(settings.NightLampScale, Is.GreaterThan(1f));
            Assert.That(settings.NightGlowScale, Is.GreaterThan(1f));
            Assert.That(settings.NightWindowLightScale, Is.LessThan(.5f));
            Assert.That(settings.NightFillScale, Is.LessThan(1f));
            Assert.That(settings.NightInteriorAmbientScale, Is.LessThan(1f));
        }

        [TestCase("WindowAmberGlow")]
        [TestCase("LanternAmberV2")]
        [TestCase("Glow")]
        [TestCase("PendantLight")]
        public void Town_GlowMaterialsShineBrighterAtNight(string material)
        {
            Assert.That(settings.GlowMaterials.Select(item => item.name), Has.Member(material));
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
