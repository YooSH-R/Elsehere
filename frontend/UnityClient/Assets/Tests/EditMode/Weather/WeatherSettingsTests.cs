using NUnit.Framework;
using UnityEngine;

namespace A305.Weather.Tests
{
    public class WeatherSettingsTests
    {
        private WeatherSettings settings;

        [SetUp]
        public void SetUp() => settings = ScriptableObject.CreateInstance<WeatherSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [Test]
        public void LookFor_Clear_KeepsDesignedScene()
        {
            var designed = new WeatherLook { sunIntensity = 1.7f, skyZenith = Color.cyan };

            WeatherLook look = settings.LookFor(WeatherKind.Clear, designed);

            Assert.That(look.sunIntensity, Is.EqualTo(1.7f));
            Assert.That(look.skyZenith, Is.EqualTo(Color.cyan));
        }

        [TestCase(WeatherKind.Cloudy)]
        [TestCase(WeatherKind.Rain)]
        public void LookFor_CloudyOrRain_DimsSunAndSkyBelowClearDay(WeatherKind kind)
        {
            WeatherLook clear = ClearSummerDay();

            WeatherLook look = settings.LookFor(kind, clear);

            Assert.That(look.sunIntensity, Is.LessThan(clear.sunIntensity));
            Assert.That(look.sunShadowStrength, Is.LessThan(clear.sunShadowStrength));
            Assert.That(look.skyZenith.grayscale, Is.LessThan(clear.skyZenith.grayscale));
            Assert.That(look.ambientSky.grayscale, Is.LessThan(clear.ambientSky.grayscale));
            Assert.That(look.cloudColor.grayscale, Is.LessThan(clear.cloudColor.grayscale));
        }

        [TestCase(WeatherKind.Cloudy)]
        [TestCase(WeatherKind.Rain)]
        public void LookFor_CloudyOrRain_StaysDaytimeNotNight(WeatherKind kind)
        {
            WeatherLook look = settings.LookFor(kind, ClearSummerDay());

            Assert.That(look.sunIntensity, Is.GreaterThan(.3f));
            Assert.That(look.skyZenith.grayscale, Is.GreaterThan(.25f));
            Assert.That(look.ambientSky.grayscale, Is.GreaterThan(.25f));
            Assert.That(look.fogEndMeters, Is.GreaterThan(look.fogStartMeters));
        }

        [Test]
        public void LookFor_Rain_IsDarkerAndFoggierThanCloudy()
        {
            WeatherLook clear = ClearSummerDay();

            WeatherLook cloudy = settings.LookFor(WeatherKind.Cloudy, clear);
            WeatherLook rain = settings.LookFor(WeatherKind.Rain, clear);

            Assert.That(rain.sunIntensity, Is.LessThan(cloudy.sunIntensity));
            Assert.That(rain.skyZenith.grayscale, Is.LessThan(cloudy.skyZenith.grayscale));
            Assert.That(rain.fogEndMeters, Is.LessThan(cloudy.fogEndMeters));
        }

        // MainstreetAtmosphere가 만드는 마을의 맑은 여름 하늘과 같은 값.
        private static WeatherLook ClearSummerDay() =>
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
