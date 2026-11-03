using NUnit.Framework;
using UnityEngine;

namespace A305.Weather.Tests
{
    public class DaylightSettingsTests
    {
        private const double Day = 24 * 60;

        private DaylightSettings settings;

        [SetUp]
        public void SetUp() => settings = DaylightSettings.Create();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        // 해는 6시에 뜨고 19시에 지며, 앞뒤 30분씩 서서히 바뀐다.
        [TestCase(12, 0, 0f)]
        [TestCase(0, 0, 1f)]
        [TestCase(5, 30, 1f)]
        [TestCase(6, 0, .5f)]
        [TestCase(6, 30, 0f)]
        [TestCase(18, 30, 0f)]
        [TestCase(19, 0, .5f)]
        [TestCase(19, 30, 1f)]
        public void NightAmount_FollowsSunriseAndSunset(int hour, int minute, float expected)
        {
            Assert.That(
                settings.NightAmount(Minutes(hour, minute)),
                Is.EqualTo(expected).Within(.001f)
            );
        }

        [Test]
        public void NightAmount_SameTimeOnLaterDay_IsSame()
        {
            Assert.That(
                settings.NightAmount(Day * 3 + Minutes(0, 0)),
                Is.EqualTo(1f).Within(.001f)
            );
            Assert.That(
                settings.NightAmount(Day * 3 + Minutes(12, 0)),
                Is.EqualTo(0f).Within(.001f)
            );
        }

        [TestCase(6, 0, 1f)]
        [TestCase(19, 0, 1f)]
        [TestCase(18, 45, .5f)]
        [TestCase(12, 0, 0f)]
        [TestCase(0, 0, 0f)]
        public void TwilightAmount_PeaksWhenSunRisesOrSets(int hour, int minute, float expected)
        {
            Assert.That(
                settings.TwilightAmount(Minutes(hour, minute)),
                Is.EqualTo(expected).Within(.001f)
            );
        }

        [Test]
        public void Mix_Midday_KeepsDayLook()
        {
            WeatherLook day = ClearDay();

            WeatherLook look = settings.Mix(day, Minutes(12, 30), skyCovered: false);

            Assert.That(look.sunIntensity, Is.EqualTo(day.sunIntensity).Within(.001f));
            AssertColor(look.skyZenith, day.skyZenith);
            AssertColor(look.ambientSky, day.ambientSky);
        }

        [Test]
        public void Mix_Midnight_DarkensSkyAndLightButKeepsWeatherFog()
        {
            WeatherLook day = ClearDay();
            day.fogStartMeters = 18f;
            day.fogEndMeters = 80f;

            WeatherLook look = settings.Mix(day, Minutes(0, 0), skyCovered: false);

            Assert.That(look.sunIntensity, Is.LessThan(day.sunIntensity * .3f));
            Assert.That(look.skyZenith.grayscale, Is.LessThan(.15f));
            Assert.That(look.ambientSky.grayscale, Is.LessThan(day.ambientSky.grayscale * .5f));
            Assert.That(look.cloudColor.grayscale, Is.LessThan(day.cloudColor.grayscale * .5f));
            Assert.That(look.fogStartMeters, Is.EqualTo(18f));
            Assert.That(look.fogEndMeters, Is.EqualTo(80f));
        }

        [Test]
        public void Mix_Sunset_TintsHorizonAndSunWarm()
        {
            WeatherLook day = ClearDay();

            WeatherLook sunset = settings.Mix(day, Minutes(19, 0), skyCovered: false);
            WeatherLook midnight = settings.Mix(day, Minutes(0, 0), skyCovered: false);

            Assert.That(Warmth(sunset.skyHorizon), Is.GreaterThan(Warmth(day.skyHorizon)));
            Assert.That(Warmth(sunset.skyHorizon), Is.GreaterThan(Warmth(midnight.skyHorizon)));
            Assert.That(Warmth(sunset.sunColor), Is.GreaterThan(Warmth(day.sunColor)));
        }

        [Test]
        public void Mix_SunsetUnderClouds_IsLessWarmThanClearSunset()
        {
            WeatherLook day = ClearDay();

            WeatherLook clear = settings.Mix(day, Minutes(19, 0), skyCovered: false);
            WeatherLook covered = settings.Mix(day, Minutes(19, 0), skyCovered: true);

            Assert.That(Warmth(covered.skyHorizon), Is.LessThan(Warmth(clear.skyHorizon)));
        }

        [TestCase(0, 0, false, 1f)]
        [TestCase(0, 0, true, 0f)]
        [TestCase(12, 0, false, 0f)]
        [TestCase(19, 0, false, 0f)]
        [TestCase(6, 0, false, 0f)]
        public void StarAmount_OnlyClearNight(int hour, int minute, bool skyCovered, float expected)
        {
            Assert.That(
                settings.StarAmount(Minutes(hour, minute), skyCovered),
                Is.EqualTo(expected).Within(.001f)
            );
        }

        [Test]
        public void SunRotation_Midday_UsesDesignedDirection()
        {
            Quaternion designed = Quaternion.Euler(48f, -35f, 0f);

            Quaternion rotation = settings.SunRotation(designed, Minutes(12, 30));

            Assert.That(Quaternion.Angle(rotation, designed), Is.LessThan(1f));
        }

        [Test]
        public void SunRotation_AfterSunrise_IsLowNearHorizon()
        {
            Quaternion designed = Quaternion.Euler(48f, -35f, 0f);

            Quaternion morning = settings.SunRotation(designed, Minutes(6, 30));

            Assert.That(Elevation(morning), Is.GreaterThan(0f).And.LessThan(20f));
        }

        [Test]
        public void SunRotation_SunriseAndSunset_ComeFromOppositeSides()
        {
            Quaternion designed = Quaternion.Euler(48f, -35f, 0f);

            Vector3 morning = settings.SunRotation(designed, Minutes(6, 30)) * Vector3.forward;
            Vector3 evening = settings.SunRotation(designed, Minutes(18, 30)) * Vector3.forward;

            Assert.That(Vector3.Dot(Flat(morning), Flat(evening)), Is.LessThan(0f));
        }

        [Test]
        public void SunRotation_AtSunset_StaysLowForLongShadows()
        {
            Quaternion designed = Quaternion.Euler(48f, -35f, 0f);

            Quaternion sunset = settings.SunRotation(designed, Minutes(19, 0));

            Assert.That(Elevation(sunset), Is.GreaterThan(0f).And.LessThan(15f));
        }

        [Test]
        public void SunRotation_Midnight_MoonlightFromDesignedDirection()
        {
            Quaternion designed = Quaternion.Euler(48f, -35f, 0f);

            Quaternion rotation = settings.SunRotation(designed, Minutes(0, 0));

            Assert.That(Quaternion.Angle(rotation, designed), Is.LessThan(1f));
        }

        private static double Minutes(int hour, int minute) => hour * 60 + minute;

        private static float Warmth(Color color) => color.r - color.b;

        private static float Elevation(Quaternion rotation) =>
            Mathf.Asin(-(rotation * Vector3.forward).y) * Mathf.Rad2Deg;

        private static Vector3 Flat(Vector3 direction) =>
            new Vector3(direction.x, 0f, direction.z).normalized;

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.002f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.002f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.002f));
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
