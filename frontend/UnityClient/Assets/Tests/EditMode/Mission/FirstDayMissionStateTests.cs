using System.Collections.Generic;
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
