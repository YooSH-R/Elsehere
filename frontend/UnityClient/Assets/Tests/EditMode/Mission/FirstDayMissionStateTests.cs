using System.Collections.Generic;
using A305.Api;
using A305.Weather;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class FirstDayMissionStateTests
    {
        [Test]
        public void Destination_BeforeStart_IsNone()
        {
            FirstDayMissionState state = State(started: false, completed: false);

            Assert.That(state.Destination, Is.Null);
        }

        [Test]
        public void Destination_InProgress_IsCurrentStepPlace()
        {
            FirstDayMissionState state = State(started: true, completed: false);

            Assert.That(state.Destination, Is.EqualTo("SHOP"));
        }

        [Test]
        public void Destination_InProgress_IgnoresVisitedScene()
        {
            FirstDayMissionState state = State(started: true, completed: false);
            state.Scene = "TOWN";

            Assert.That(state.Destination, Is.EqualTo("SHOP"));
        }

        [Test]
        public void Destination_AfterCompletion_IsNone()
        {
            FirstDayMissionState state = State(started: true, completed: true);

            Assert.That(state.Destination, Is.Null);
        }

        [TestCase("CLEAR", WeatherKind.Clear)]
        [TestCase("CLOUDY", WeatherKind.Cloudy)]
        [TestCase("RAIN", WeatherKind.Rain)]
        public void Forecast_ServerWeather_DecidesTownWeather(string weather, WeatherKind expected)
        {
            FirstDayMissionState state = State(started: true, completed: false);
            state.Weather = weather;

            Assert.That(state.Forecast, Is.EqualTo(expected));
        }

        [Test]
        public void Forecast_ServerWeather_WinsOverRaining()
        {
            FirstDayMissionState state = State(started: true, completed: false);
            state.Raining = true;
            state.Weather = "CLOUDY";

            Assert.That(state.Forecast, Is.EqualTo(WeatherKind.Cloudy));
        }

        [TestCase(true, WeatherKind.Rain)]
        [TestCase(false, WeatherKind.Clear)]
        public void Forecast_WithoutServerWeather_FollowsRaining(bool raining, WeatherKind expected)
        {
            FirstDayMissionState state = State(started: true, completed: false);
            state.Raining = raining;

            Assert.That(state.Forecast, Is.EqualTo(expected));
        }

        [Test]
        public void Forecast_UnknownServerWeather_FollowsRaining()
        {
            FirstDayMissionState state = State(started: true, completed: false);
            state.Raining = true;
            state.Weather = "SNOW";

            Assert.That(state.Forecast, Is.EqualTo(WeatherKind.Rain));
        }

        [Test]
        public void Forecast_ReadsWeatherFieldOfServerResponse()
        {
            FirstDayMissionState state = ApiJson.Deserialize<FirstDayMissionState>(
                "{\"started\":true,\"stage\":3,\"raining\":false,\"weather\":\"CLOUDY\"}"
            );

            Assert.That(state.Forecast, Is.EqualTo(WeatherKind.Cloudy));
        }

        private static FirstDayMissionState State(bool started, bool completed) =>
            new FirstDayMissionState
            {
                Started = started,
                Completed = completed,
                Scene = "HOTEL",
                Steps = new List<FirstDayMissionStep>
                {
                    new FirstDayMissionStep { Scene = "TOWN", Status = "DONE" },
                    new FirstDayMissionStep { Scene = "SHOP", Status = "CURRENT" },
                    new FirstDayMissionStep { Scene = "HOTEL", Status = "UPCOMING" },
                },
            };
    }
}
