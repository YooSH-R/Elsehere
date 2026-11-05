using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.World;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.PlayModeTests
{
    internal static class NpcTestClock
    {
        // Ben·Ruby·Dan이 일과 중이고, 거리 매장도 문을 연 시각이다.
        public static void SetDaytime() => SetTime(12 * 60 + 15);

        public static void SetTime(int minuteOfDay, int day = 1)
        {
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var api = new FixedClock(minuteOfDay, day);
            Assert.That(
                GameClock.Shared.TickAsync(api, Time.realtimeSinceStartupAsDouble + 1e5).Result,
                Is.EqualTo(GameClockBeatStatus.Updated)
            );
        }

        public static IEnumerator OpenPoliceStation()
        {
            var station = Object
                .FindObjectsByType<ShopInterior>(FindObjectsSortMode.None)
                .Single(room => room.PlaceCode == "POLICE_STATION");
            yield return (IEnumerator)
                typeof(ShopInterior)
                    .GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(station, new object[] { true });
            yield return null;
        }

        public static void Restore()
        {
            foreach (
                var driver in Object.FindObjectsByType<GameClockDriver>(FindObjectsSortMode.None)
            )
                driver.enabled = false;
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
        }

        private sealed class FixedClock : IGameClockApi
        {
            private readonly int minuteOfDay;
            private readonly int day;

            public FixedClock(int minuteOfDay, int day)
            {
                this.minuteOfDay = minuteOfDay;
                this.day = day;
            }

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            ) =>
                Task.FromResult(
                    new GameClockTime
                    {
                        Day = day,
                        MinuteOfDay = minuteOfDay,
                        Paused = true,
                    }
                );
        }
    }
}
