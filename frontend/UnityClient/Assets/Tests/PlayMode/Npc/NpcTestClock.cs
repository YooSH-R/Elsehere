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
        public static void SetDaytime()
        {
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var api = new FixedClock();
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
            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            ) =>
                Task.FromResult(
                    new GameClockTime
                    {
                        Day = 1,
                        MinuteOfDay = 12 * 60 + 15,
                        Paused = true,
                    }
                );
        }
    }
}
