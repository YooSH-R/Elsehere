using System;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 사용자의 게임 시각과 서버 신호 주기(WORLD-07). 게임 시간은 서버가 사용자마다 플레이한 시간만큼 흐르게 하며, 월드 장면에 있는 동안
    /// 30초마다 신호를 보낸다. 장면을 옮겨도 신호 주기가 이어지도록 게임 전체가 <see cref="Shared"/>를 함께 쓴다.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>신호 간격. 서버는 신호 사이를 한 번에 60초까지만 세므로 그보다 짧아야 한다.</summary>
        public const double BeatIntervalSeconds = 30;

        /// <summary>실제 600초가 게임 하루다(서버 GameTime.REAL_SECONDS_PER_DAY).</summary>
        public const double GameMinutesPerRealSecond = MinutesPerDay / 600;

        private const double MinutesPerDay = 24 * 60;
        private const double StartMinutes = 8 * 60;

        public static GameClock Shared { get; private set; } = new GameClock();

        // 다음 신호를 보낼 실제 시각(Time.realtimeSinceStartupAsDouble). 처음에는 바로 보낸다.
        private double nextBeatAt;
        private bool waiting;

        /// <summary>서버가 마지막으로 알려 준 게임 시각. 아직 받지 못했으면 null.</summary>
        public GameClockTime Current { get; private set; }

        /// <summary>
        /// 1일차 0시부터 센 지금 게임 시각(분). 신호 사이에도 실제 시간만큼 흐르고, 서버에서 새 시각을 받으면 그 값으로 맞춘다.
        /// 서버 시각을 받기 전(게스트, 서버 없이 씬만 볼 때)에는 새 사용자처럼 1일차 오전 8시에서 시작한다.
        /// </summary>
        public double Minutes { get; private set; } = StartMinutes;

        /// <summary>튜토리얼처럼 진행 흐름이 게임 시간을 멈춰 두는 동안 true.</summary>
        public bool IsHeld { get; set; }

        /// <summary>
        /// 실제 시간 realSeconds만큼 게임 시각을 흘린다. 진행 흐름이 시계를 붙잡아 두었거나, 서버가 대화 중·튜토리얼 중이라 멈췄다고 알려 주면 흘리지 않는다.
        /// </summary>
        public void Flow(double realSeconds)
        {
            if (IsHeld || (Current != null && Current.Paused))
            {
                return;
            }

            Minutes += realSeconds * GameMinutesPerRealSecond;
        }

        /// <summary>마지막 신호가 실패한 이유. 실패하지 않았으면 null.</summary>
        public ApiException LastError { get; private set; }

        /// <summary>서버에서 새 게임 시각을 받았다.</summary>
        public event Action Changed;

        /// <summary>지금 신호를 보낼 때인지. 앞선 신호의 응답을 기다리는 동안은 보내지 않는다.</summary>
        public bool IsDue(double now)
        {
            return !waiting && now >= nextBeatAt;
        }

        /// <summary>
        /// 보낼 때면 신호를 보내고 받은 게임 시각으로 바꾼다. 실패해도 예외를 던지지 않고 마지막 게임 시각을 그대로 두며, 다음 간격에 다시 보낸다.
        /// </summary>
        /// <param name="now">실제 시각(초)</param>
        public async Task<GameClockBeatStatus> TickAsync(IGameClockApi api, double now)
        {
            if (!IsDue(now))
            {
                return GameClockBeatStatus.NotDue;
            }

            waiting = true;
            nextBeatAt = now + BeatIntervalSeconds;
            try
            {
                GameClockTime time = await api.BeatAsync();
                LastError = null;
                Current = time;
                Minutes = (time.Day - 1) * MinutesPerDay + time.MinuteOfDay;
                Changed?.Invoke();
                return GameClockBeatStatus.Updated;
            }
            catch (ApiException exception)
            {
                LastError = exception;
                return GameClockBeatStatus.Failed;
            }
            finally
            {
                waiting = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared()
        {
            Shared = new GameClock();
        }
    }
}
