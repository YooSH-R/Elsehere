using NUnit.Framework;
using UnityEngine;

namespace A305.Weather.Tests
{
    public class WeatherLookTests
    {
        [Test]
        public void Lerp_Halfway_MixesSkySunFogAndClouds()
        {
            WeatherLook mixed = WeatherLook.Lerp(Bright(), Dark(), .5f);

            Assert.That(mixed.skyZenith, Is.EqualTo(Color.gray));
            Assert.That(mixed.skyHorizon, Is.EqualTo(Color.gray));
            Assert.That(mixed.sunColor, Is.EqualTo(Color.gray));
            Assert.That(mixed.sunIntensity, Is.EqualTo(1f));
            Assert.That(mixed.sunShadowStrength, Is.EqualTo(.5f));
            Assert.That(mixed.ambientSky, Is.EqualTo(Color.gray));
            Assert.That(mixed.ambientEquator, Is.EqualTo(Color.gray));
            Assert.That(mixed.ambientGround, Is.EqualTo(Color.gray));
            Assert.That(mixed.fogColor, Is.EqualTo(Color.gray));
            Assert.That(mixed.fogStartMeters, Is.EqualTo(30f));
            Assert.That(mixed.fogEndMeters, Is.EqualTo(90f));
            Assert.That(mixed.cloudColor, Is.EqualTo(Color.gray));
        }

        [Test]
        public void Lerp_PastTheEnd_StopsAtTarget()
        {
            WeatherLook mixed = WeatherLook.Lerp(Bright(), Dark(), 1.5f);

            Assert.That(mixed.sunIntensity, Is.EqualTo(0f));
            Assert.That(mixed.fogEndMeters, Is.EqualTo(60f));
            Assert.That(mixed.skyZenith, Is.EqualTo(Color.black));
        }

        private static WeatherLook Bright() =>
            new WeatherLook
            {
                skyZenith = Color.white,
                skyHorizon = Color.white,
                sunColor = Color.white,
                sunIntensity = 2f,
                sunShadowStrength = 1f,
                ambientSky = Color.white,
                ambientEquator = Color.white,
                ambientGround = Color.white,
                fogColor = Color.white,
                fogStartMeters = 40f,
                fogEndMeters = 120f,
                cloudColor = Color.white,
            };

        private static WeatherLook Dark() =>
            new WeatherLook
            {
                skyZenith = Color.black,
                skyHorizon = Color.black,
                sunColor = Color.black,
                sunIntensity = 0f,
                sunShadowStrength = 0f,
                ambientSky = Color.black,
                ambientEquator = Color.black,
                ambientGround = Color.black,
                fogColor = Color.black,
                fogStartMeters = 20f,
                fogEndMeters = 60f,
                cloudColor = Color.black,
            };
    }
}
