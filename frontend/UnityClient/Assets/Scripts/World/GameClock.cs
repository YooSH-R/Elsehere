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

        /// <summary>한 프레임에서 플레이한 시간으로 세는 최대 실제 시간(초). 이보다 긴 프레임은 Unity가 멈춰 있던 시간이 섞인 것이다.</summary>
        public const double MaxFrameSeconds = 1;

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
        public bool IsHeld
        {
            get => held;
            set
            {
                if (held != value)
                {
                    held = value;
                    PauseChanged(value);
                }
            }
        }

        private bool held;
        private bool learningOpen;
        private bool dialogueOpen;
        private bool attemptedPause;
        private int pauseRevision;
        private int serverTimeRevision;
        private bool pauseSinceBeat;
        private double activeSecondsSinceBeat;

        public bool IsLearningWindowOpen
        {
            get => learningOpen;
            set
            {
                if (learningOpen != value)
                {
                    learningOpen = value;
                    PauseChanged(value);
                }
            }
        }

        public bool IsTalking
        {
            get => dialogueOpen;
            set
            {
                if (dialogueOpen != value)
                {
                    dialogueOpen = value;
                    PauseChanged(value);
                }
            }
        }

        private void PauseChanged(bool open)
        {
            pauseSinceBeat |= open;
            RequestSynchronization();
        }

        /// <summary>서버 상태가 바뀌면 이전 정지 상태를 버리고 다음 프레임에 시각을 확인한다.</summary>
        public void RequestSynchronization()
        {
            pauseRevision++;
            nextBeatAt = 0;
        }

        private bool InteractionPaused => held || learningOpen || dialogueOpen;

        /// <summary>대화·학습 창이 열려 게임 시각이 멈춰 있는지. 플레이어 기준 시간(NPC 말 걸기 간격 등)을 셀 때 이 동안은 뺀다.</summary>
        public bool IsInteractionPaused => InteractionPaused;

        /// <summary>
        /// 실제 시간 realSeconds만큼 게임 시각을 흘린다. 진행 흐름이 시계를 붙잡아 두었거나, 서버가 대화 중·튜토리얼 중이라 멈췄다고 알려 주면 흘리지 않는다.
        /// </summary>
        public void Flow(double realSeconds)
        {
            if (
                InteractionPaused
                || (Current != null && Current.Paused && serverTimeRevision == pauseRevision)
            )
            {
                return;
            }

            activeSecondsSinceBeat += realSeconds;
            Minutes += realSeconds * GameMinutesPerRealSecond;
        }

        /// <summary>
        /// 한 프레임 동안 흐른 실제 시간만큼 게임 시각을 흘리되, 한 프레임에서는 <see cref="MaxFrameSeconds"/>까지만 센다.
        /// 창이 포커스를 잃거나 탭이 숨으면 Unity가 멈췄다가 돌아온 첫 프레임에 떠나 있던 시간이 한꺼번에 들어오는데,
        /// 그 시간은 플레이한 시간이 아니다. 그대로 더하면 시계와 NPC 일과가 몇 시간 앞으로 튀었다가 서버 응답에서 되돌아간다.
        /// </summary>
        public void FlowFrame(double frameSeconds)
        {
            Flow(Math.Min(frameSeconds, MaxFrameSeconds));
        }

        /// <summary>마지막 신호가 실패한 이유. 실패하지 않았으면 null.</summary>
        public ApiException LastError { get; private set; }

        /// <summary>서버에서 새 게임 시각을 받았다.</summary>
        public event Action Changed;

        /// <summary>지금 신호를 보낼 때인지. 앞선 신호의 응답을 기다리는 동안은 보내지 않는다.</summary>
        public bool IsDue(double now)
        {
            return !waiting && (now >= nextBeatAt || InteractionPaused != attemptedPause);
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
            long activeSeconds = (long)Math.Floor(activeSecondsSinceBeat);
            // 보낸 진행 초는 빼 두고, 응답을 기다리는 동안 흐르는 시간만 새로 모은다.
            activeSecondsSinceBeat -= activeSeconds;
            int revision = pauseRevision;
            bool paused = InteractionPaused;
            bool wasPaused = pauseSinceBeat;
            pauseSinceBeat = paused;
            attemptedPause = paused;
            nextBeatAt = now + BeatIntervalSeconds;
            try
            {
                GameClockTime time = await api.BeatAsync(paused, wasPaused, activeSeconds);
                LastError = null;
                if (revision == pauseRevision)
                {
                    Current = time;
                    serverTimeRevision = revision;
                    // 서버 시각은 요청을 받은 때의 시각이다. 아직 보내지 않은 진행 초(응답을 기다리는 동안 흐른 시간)를
                    // 더해, 응답이 늦어도 시계가 뒤로 튀지 않게 한다. 실제 2초가 게임 5분이라 NPC 출퇴근이 한꺼번에 다시 놓인다.
                    Minutes =
                        (time.Day - 1) * MinutesPerDay
                        + time.MinuteOfDay
                        + activeSecondsSinceBeat * GameMinutesPerRealSecond;
                }
                Changed?.Invoke();
                return GameClockBeatStatus.Updated;
            }
            catch (ApiException exception)
            {
                pauseSinceBeat |= wasPaused;
                // 실패한 신호의 진행 초는 다음 신호에 함께 보낸다. 서버가 받았는데 응답만 실패했더라도 서버는 지난 신호부터
                // 실제로 지난 시간까지만 더하므로 두 번 더해지지 않는다. 버리면 서버 시각이 그만큼 늦어져 다음 신호에서 크게 뒤로 튄다.
                activeSecondsSinceBeat += activeSeconds;
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
