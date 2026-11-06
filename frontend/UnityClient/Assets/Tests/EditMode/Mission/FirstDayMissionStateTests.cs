using System.Collections.Generic;
using A305.Api;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class FirstDayMissionStateTests
    {
        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void ShouldOfferTownStory_OnlyAfterFirstDayCompletes(
            bool completed,
            bool isTownStory,
            bool expected
        )
        {
            var state = new FirstDayMissionState
            {
                Completed = completed,
                IsTownStory = isTownStory,
            };
            Assert.That(state.ShouldOfferTownStory, Is.EqualTo(expected));
        }

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

        [Test]
        public void CurrentNpcCode_InProgress_IsTheNpcOfTheCurrentStep()
        {
            FirstDayMissionState state = State(started: true, completed: false);

            Assert.That(state.CurrentNpcCode, Is.EqualTo("SHOP_STAFF"));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void CurrentNpcCode_BeforeStartOrAfterCompletion_IsNone(bool started, bool completed)
        {
            FirstDayMissionState state = State(started, completed);

            Assert.That(state.CurrentNpcCode, Is.Null);
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

        // 카페 의자는 호텔로 돌아가기 전까지 원하는 만큼 앉을 수 있다. 쉬고 난 뒤(2단계)와 주문 전에도 앉는다.
        [TestCase("CAFE", 1, true, false, false, true)]
        [TestCase("CAFE", 1, false, false, false, true)]
        [TestCase("CAFE", 2, false, false, false, true)]
        [TestCase("HOTEL", 2, false, false, false, false)]
        [TestCase("CAFE", 1, true, true, false, false)]
        [TestCase("CAFE", 1, true, false, true, false)]
        public void CanSitInCafe_AnyTimeInCafeWhileIdle(
            string scene,
            int stage,
            bool canRest,
            bool busy,
            bool talking,
            bool expected
        )
        {
            var state = new FirstDayMissionState
            {
                Started = true,
                Stage = stage,
                CanRest = canRest,
            };

            Assert.That(state.CanSitInCafe(scene, busy, talking), Is.EqualTo(expected));
        }

        // 미션은 카페 의자에서 쉰 것만 휴식으로 센다. 마을 벤치·호텔 소파는 미션이 막거나 세지 않는다.
        [TestCase("CAFE", true)]
        [TestCase("TOWN", false)]
        [TestCase("HOTEL", false)]
        public void ManagesSeatsIn_OnlyCafe(string scene, bool expected)
        {
            Assert.That(FirstDayMissionState.ManagesSeatsIn(scene), Is.EqualTo(expected));
        }

        // 휴식으로 미션이 넘어가는 것은 주문을 마친 뒤 처음 쉬는 한 번뿐이다.
        [TestCase(true, false, 1, true, true)]
        [TestCase(true, false, 1, false, false)]
        [TestCase(true, false, 2, false, false)]
        [TestCase(true, true, 1, true, false)]
        [TestCase(false, false, 1, true, false)]
        public void CountsAsCafeRest_OnlyAfterOrderAndBeforeRest(
            bool started,
            bool completed,
            int stage,
            bool canRest,
            bool expected
        )
        {
            var state = new FirstDayMissionState
            {
                Started = started,
                Completed = completed,
                Stage = stage,
                CanRest = canRest,
            };

            Assert.That(state.CountsAsCafeRest, Is.EqualTo(expected));
        }

        // 하던 퀘스트를 끝내면(다음 퀘스트를 기다리거나 모두 마침) 그 퀘스트를 알린다. 처음 받은 상태, 계속하는 퀘스트, 시작 전은 끝낸 것이 아니다.
        [Test]
        public void QuestFinishedSince_NamesTheQuestThatWasInProgress()
        {
            FirstDayMissionState doing = Quest("MORNING_AND_HAIRCUT", started: true);

            Assert.That(doing.QuestFinishedSince(null), Is.Null, "처음 받은 상태");
            Assert.That(
                Quest("MORNING_AND_HAIRCUT", started: true).QuestFinishedSince(doing),
                Is.Null,
                "계속하는 중"
            );
            Assert.That(
                Quest("THANKS_AND_PARCEL", started: false).QuestFinishedSince(doing),
                Is.EqualTo("MORNING_AND_HAIRCUT"),
                "다음 퀘스트를 기다림"
            );
            Assert.That(
                Quest("MORNING_AND_HAIRCUT", started: false)
                    .QuestFinishedSince(Quest("CHECK_IN", started: true)),
                Is.EqualTo("CHECK_IN"),
                "첫날 퀘스트를 끝내고 마을 퀘스트 시작 전"
            );
            FirstDayMissionState everything = Quest("THANKS_AND_PARCEL", started: true);
            everything.Completed = true;
            Assert.That(
                everything.QuestFinishedSince(Quest("THANKS_AND_PARCEL", started: true)),
                Is.EqualTo("THANKS_AND_PARCEL"),
                "모두 마침"
            );
            Assert.That(
                Quest("CHECK_IN", started: false)
                    .QuestFinishedSince(Quest("CHECK_IN", started: false)),
                Is.Null,
                "시작 전"
            );
        }

        private static FirstDayMissionState Quest(string quest, bool started) =>
            new FirstDayMissionState { Quest = quest, Started = started };

        private static FirstDayMissionState State(bool started, bool completed) =>
            new FirstDayMissionState
            {
                Started = started,
                Completed = completed,
                Scene = "HOTEL",
                Steps = new List<FirstDayMissionStep>
                {
                    new FirstDayMissionStep
                    {
                        Scene = "TOWN",
                        NpcCode = "TOWN_PASSERBY",
                        Status = "DONE",
                    },
                    new FirstDayMissionStep
                    {
                        Scene = "SHOP",
                        NpcCode = "SHOP_STAFF",
                        Status = "CURRENT",
                    },
                    new FirstDayMissionStep
                    {
                        Scene = "HOTEL",
                        NpcCode = "HOTEL_STAFF",
                        Status = "UPCOMING",
                    },
                },
            };
    }
}
