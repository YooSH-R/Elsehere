using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace A305.World.PlayModeTests
{
    /// <summary>
    /// 시험이 정한 게임 시각으로 시계를 맞춘다. 서버 신호를 받은 것처럼 넣으므로 멈춘 시각(대화 중과 같음)도 줄 수 있다.
    /// 영업시간처럼 시각에 따라 문과 장소가 달라지는 시험은 앞선 시험이 흘린 시각과 상관없이 이 시각에서 시작한다.
    /// </summary>
    internal static class TestGameClock
    {
        // 장면의 신호가 시험이 정한 시각을 덮어쓰지 않게 다음 신호를 하루 넘게 뒤로 미룬다.
        // 다른 시험이 같은 방식으로 시각을 맞출 때 막히지 않도록 그보다 크게 미루지는 않는다.
        private const double BeatDelaySeconds = 1e5;

        public static void Set(int minuteOfDay, bool paused = true)
        {
            // 앞선 시험이 미뤄 둔 신호와 상관없이 맞추도록, Play를 새로 시작할 때처럼 시계를 비운다.
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var time = new GameClockTime
            {
                Day = 1,
                MinuteOfDay = minuteOfDay,
                Paused = paused,
            };
            Task<GameClockBeatStatus> beat = GameClock.Shared.TickAsync(
                new FixedGameClockApi(time),
                Time.realtimeSinceStartupAsDouble + BeatDelaySeconds
            );
            Assert.That(beat.Result, Is.EqualTo(GameClockBeatStatus.Updated));
        }

        /// <summary>다른 시험처럼 1일차 오전 8시부터 흐르는 시계로 돌린다.</summary>
        public static void Restore()
        {
            Set(8 * 60, paused: false);
        }

        private sealed class FixedGameClockApi : IGameClockApi
        {
            private readonly GameClockTime time;

            public FixedGameClockApi(GameClockTime time)
            {
                this.time = time;
            }

            public Task<GameClockTime> BeatAsync()
            {
                return Task.FromResult(time);
            }
        }
    }
}
