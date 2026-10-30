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

        public static GameClock Shared { get; private set; } = new GameClock();

        // 다음 신호를 보낼 실제 시각(Time.realtimeSinceStartupAsDouble). 처음에는 바로 보낸다.
        private double nextBeatAt;
        private bool waiting;

        /// <summary>서버가 마지막으로 알려 준 게임 시각. 아직 받지 못했으면 null.</summary>
        public GameClockTime Current { get; private set; }

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
