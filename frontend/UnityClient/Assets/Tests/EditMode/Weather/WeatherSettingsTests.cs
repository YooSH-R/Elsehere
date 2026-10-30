using NUnit.Framework;
using UnityEngine;

namespace A305.Weather.Tests
{
    public class WeatherSettingsTests
    {
        private WeatherDefinition clear;
        private WeatherDefinition rain;
        private WeatherSettings settings;

        [SetUp]
        public void SetUp()
        {
            clear = WeatherDefinition.Create("CLEAR", default, keepDesignedLook: true);
            rain = WeatherDefinition.Create("RAIN", default, coversSky: true);
            settings = WeatherSettings.Create(1.5f, 7f, clear, rain);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(clear);
            Object.DestroyImmediate(rain);
        }

        [Test]
        public void Resolve_ServerWeather_FindsWeatherByCode()
        {
            Assert.That(settings.Resolve("RAIN", "CLEAR"), Is.SameAs(rain));
        }

        [Test]
        public void Resolve_ServerWeather_IgnoresLetterCase()
        {
            Assert.That(settings.Resolve("rain", "CLEAR"), Is.SameAs(rain));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("SNOW")]
        public void Resolve_MissingOrUnknownWeather_UsesFallback(string code)
        {
            Assert.That(settings.Resolve(code, "CLEAR"), Is.SameAs(clear));
        }

        [Test]
        public void Resolve_NeitherWeatherKnown_FindsNothing()
        {
            Assert.That(settings.Resolve("SNOW", "HAIL"), Is.Null);
        }

        [Test]
        public void Resolve_NewWeatherAsset_IsFoundWithoutCodeChanges()
        {
            WeatherDefinition snow = WeatherDefinition.Create("SNOW", default, coversSky: true);
            WeatherSettings withSnow = WeatherSettings.Create(1.5f, 7f, clear, rain, snow);

            Assert.That(withSnow.Resolve("SNOW", "CLEAR"), Is.SameAs(snow));
            Assert.That(withSnow.Weathers, Has.Count.EqualTo(3));

            Object.DestroyImmediate(withSnow);
            Object.DestroyImmediate(snow);
        }
    }
}
