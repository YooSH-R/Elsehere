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

        // 서버가 이 시각에 시계를 멈춰 두었다고 알려 준 상태로 맞춘다. 서버가 멈춘 시계는 GameClock.Flow로 흐르지 않는다.
        public static void SetTime(int minuteOfDay)
        {
            // 장면 사이에 유지되는 NPC 생활도 비워, 앞선 테스트의 욕구가 남지 않게 한다.
            NpcLives.Shared.Clear();
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var api = new FixedClock(minuteOfDay);
            Assert.That(
                GameClock.Shared.TickAsync(api, Time.realtimeSinceStartupAsDouble + 1e5).Result,
                Is.EqualTo(GameClockBeatStatus.Updated)
            );
        }

        public static void Restore()
        {
            foreach (
                var driver in Object.FindObjectsByType<GameClockDriver>(FindObjectsSortMode.None)
            )
                driver.enabled = false;
            NpcLives.Shared.Clear();
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
        }

        private sealed class FixedClock : IGameClockApi
        {
            private readonly int minuteOfDay;

            public FixedClock(int minuteOfDay)
            {
                this.minuteOfDay = minuteOfDay;
            }

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            ) =>
                Task.FromResult(
                    new GameClockTime
                    {
                        Day = 1,
                        MinuteOfDay = minuteOfDay,
                        Paused = true,
                    }
                );
        }
    }
}
