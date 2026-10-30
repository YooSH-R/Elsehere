using System.Collections.Generic;
using A305.Api;
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

        [TestCase(true, "RAIN")]
        [TestCase(false, "CLEAR")]
        public void FallbackWeather_FollowsRaining(bool raining, string expected)
        {
            FirstDayMissionState state = State(started: true, completed: false);
            state.Raining = raining;

            Assert.That(state.FallbackWeather, Is.EqualTo(expected));
        }

        [Test]
        public void Weather_ReadsServerForecastAndKeepsRainingFallback()
        {
            FirstDayMissionState state = ApiJson.Deserialize<FirstDayMissionState>(
                "{\"started\":true,\"stage\":3,\"raining\":false,\"weather\":\"CLOUDY\"}"
            );

            Assert.That(state.Weather, Is.EqualTo("CLOUDY"));
            Assert.That(state.FallbackWeather, Is.EqualTo("CLEAR"));
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
