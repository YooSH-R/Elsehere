using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Weather.Tests
{
    /// <summary>마을에 실제로 쓰는 날씨 에셋이 예보마다 알맞은 모습인지 확인한다.</summary>
    public class TownWeatherAssetTests
    {
        private const string SettingsPath = "Assets/Data/Weather/TownWeather.asset";

        private WeatherSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<WeatherSettings>(SettingsPath);
            Assert.That(settings, Is.Not.Null, SettingsPath + "가 없습니다.");
        }

        [TestCase("CLEAR")]
        [TestCase("CLOUDY")]
        [TestCase("RAIN")]
        public void TownWeather_HasEveryServerForecast(string code)
        {
            Assert.That(settings.Resolve(code, null), Is.Not.Null);
        }

        [Test]
        public void Clear_KeepsDesignedSkyWithoutCloudsOrEffects()
        {
            WeatherDefinition clear = settings.Resolve("CLEAR", null);

            WeatherLook look = clear.LookOver(ClearSummerDay());

            Assert.That(look.sunIntensity, Is.EqualTo(ClearSummerDay().sunIntensity));
            Assert.That(look.skyZenith, Is.EqualTo(ClearSummerDay().skyZenith));
            Assert.That(clear.CoversSky, Is.False);
            Assert.That(clear.Effect, Is.Null);
        }

        [TestCase("CLOUDY")]
        [TestCase("RAIN")]
        public void CloudyOrRain_DimsSunAndSkyBelowClearDay(string code)
        {
            WeatherLook clear = ClearSummerDay();

            WeatherLook look = settings.Resolve(code, null).LookOver(clear);

            Assert.That(look.sunIntensity, Is.LessThan(clear.sunIntensity));
            Assert.That(look.sunShadowStrength, Is.LessThan(clear.sunShadowStrength));
            Assert.That(look.skyZenith.grayscale, Is.LessThan(clear.skyZenith.grayscale));
            Assert.That(look.ambientSky.grayscale, Is.LessThan(clear.ambientSky.grayscale));
            Assert.That(look.cloudColor.grayscale, Is.LessThan(clear.cloudColor.grayscale));
        }

        [TestCase("CLOUDY")]
        [TestCase("RAIN")]
        public void CloudyOrRain_StaysDaytimeNotNight(string code)
        {
            WeatherLook look = settings.Resolve(code, null).LookOver(ClearSummerDay());

            Assert.That(look.sunIntensity, Is.GreaterThan(.3f));
            Assert.That(look.skyZenith.grayscale, Is.GreaterThan(.25f));
            Assert.That(look.ambientSky.grayscale, Is.GreaterThan(.25f));
            Assert.That(look.fogEndMeters, Is.GreaterThan(look.fogStartMeters));
        }

        [TestCase("CLOUDY")]
        [TestCase("RAIN")]
        public void CloudyOrRain_CoversSkyWithClouds(string code)
        {
            Assert.That(settings.Resolve(code, null).CoversSky, Is.True);
        }

        [Test]
        public void Rain_IsDarkerAndFoggierThanCloudy()
        {
            WeatherLook clear = ClearSummerDay();

            WeatherLook cloudy = settings.Resolve("CLOUDY", null).LookOver(clear);
            WeatherLook rain = settings.Resolve("RAIN", null).LookOver(clear);

            Assert.That(rain.sunIntensity, Is.LessThan(cloudy.sunIntensity));
            Assert.That(rain.skyZenith.grayscale, Is.LessThan(cloudy.skyZenith.grayscale));
            Assert.That(rain.fogEndMeters, Is.LessThan(cloudy.fogEndMeters));
        }

        [Test]
        public void Rain_DropsParticlesButCloudyDoesNot()
        {
            GameObject rain = settings.Resolve("RAIN", null).Effect;

            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.GetComponentInChildren<ParticleSystem>(), Is.Not.Null);
            Assert.That(settings.Resolve("CLOUDY", null).Effect, Is.Null);
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
