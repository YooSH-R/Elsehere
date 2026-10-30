using NUnit.Framework;
using UnityEngine;

namespace A305.Weather.Tests
{
    public class WeatherDefinitionTests
    {
        private static readonly WeatherLook Designed = new WeatherLook
        {
            sunIntensity = 1.7f,
            skyZenith = Color.cyan,
        };

        private static readonly WeatherLook Gloomy = new WeatherLook
        {
            sunIntensity = .6f,
            skyZenith = Color.gray,
        };

        [Test]
        public void LookOver_KeepDesignedLook_UsesSceneDesign()
        {
            WeatherDefinition clear = WeatherDefinition.Create(
                "CLEAR",
                Gloomy,
                keepDesignedLook: true
            );

            WeatherLook look = clear.LookOver(Designed);

            Assert.That(look.sunIntensity, Is.EqualTo(1.7f));
            Assert.That(look.skyZenith, Is.EqualTo(Color.cyan));
            Object.DestroyImmediate(clear);
        }

        [Test]
        public void LookOver_OwnLook_ReplacesSceneDesign()
        {
            WeatherDefinition rain = WeatherDefinition.Create("RAIN", Gloomy);

            WeatherLook look = rain.LookOver(Designed);

            Assert.That(look.sunIntensity, Is.EqualTo(.6f));
            Assert.That(look.skyZenith, Is.EqualTo(Color.gray));
            Object.DestroyImmediate(rain);
        }
    }
}
