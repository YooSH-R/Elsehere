using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.World.Tests
{
    public class GameClockTests
    {
        [Test]
        public async Task TutorialHoldReleased_RequestsTheServerTimeImmediately()
        {
            var clock = new GameClock { IsHeld = true };
            var api = new FakeGameClockApi
            {
                Reply = new GameClockTime
                {
                    Day = 1,
                    MinuteOfDay = 480,
                    Paused = true,
                },
            };
            await clock.TickAsync(api, 5);

            clock.IsHeld = false;

            Assert.That(clock.IsDue(6), Is.True, "튜토리얼 종료 후 30초를 기다리지 않는다");
        }

        [Test]
        public async Task ClosedInteraction_IgnoresThePausedStateFromAnOlderRequest()
        {
            var clock = new GameClock { IsTalking = true };
            var api = new FakeGameClockApi { Gate = new TaskCompletionSource<GameClockTime>() };
            Task<GameClockBeatStatus> pending = clock.TickAsync(api, 5);
            clock.IsTalking = false;
            api.Gate.SetResult(
                new GameClockTime
                {
                    Day = 1,
                    MinuteOfDay = 480,
                    Paused = true,
                }
            );
            await pending;

            clock.Flow(1);

            Assert.That(
                clock.Minutes,
                Is.GreaterThan(480),
                "지난 정지 응답으로 다시 멈추지 않는다"
            );
        }

        [Test]
        public async Task TickAsync_FirstTick_SendsBeatAndKeepsServerTime()
        {
            var api = new FakeGameClockApi { Reply = Time(1, 480) };
            var clock = new GameClock();
            int changed = 0;
            clock.Changed += () => changed++;

            GameClockBeatStatus status = await clock.TickAsync(api, 5);

            Assert.That(status, Is.EqualTo(GameClockBeatStatus.Updated));
            Assert.That(api.Calls, Is.EqualTo(1));
            Assert.That(clock.Current.Day, Is.EqualTo(1));
            Assert.That(clock.Current.MinuteOfDay, Is.EqualTo(480));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public async Task TickAsync_BeforeThirtySeconds_DoesNotSendAgain()
        {
            var api = new FakeGameClockApi { Reply = Time(1, 480) };
            var clock = new GameClock();

            await clock.TickAsync(api, 5);
            GameClockBeatStatus early = await clock.TickAsync(api, 34.9);
            GameClockBeatStatus due = await clock.TickAsync(api, 35);

            Assert.That(early, Is.EqualTo(GameClockBeatStatus.NotDue));
            Assert.That(due, Is.EqualTo(GameClockBeatStatus.Updated));
            Assert.That(api.Calls, Is.EqualTo(2));
            Assert.That(clock.IsDue(64.9), Is.False);
            Assert.That(clock.IsDue(65), Is.True);
        }

        [Test]
        public async Task TickAsync_WhileWaitingForServer_DoesNotSendAgain()
        {
            var api = new FakeGameClockApi { Gate = new TaskCompletionSource<GameClockTime>() };
            var clock = new GameClock();

            Task<GameClockBeatStatus> first = clock.TickAsync(api, 5);
            GameClockBeatStatus second = await clock.TickAsync(api, 100);
            api.Gate.SetResult(Time(2, 0));
            await first;

            Assert.That(second, Is.EqualTo(GameClockBeatStatus.NotDue));
            Assert.That(api.Calls, Is.EqualTo(1));
            Assert.That(clock.Current.Day, Is.EqualTo(2));
        }

        [Test]
        public async Task TickAsync_ServerFails_KeepsLastTimeAndTriesAgainNextInterval()
        {
            var api = new FakeGameClockApi { Reply = Time(1, 480) };
            var clock = new GameClock();
            await clock.TickAsync(api, 5);
            api.Failure = new ApiException(ApiErrorCodes.NetworkError, 0, "연결 실패");

            GameClockBeatStatus status = await clock.TickAsync(api, 35);

            Assert.That(status, Is.EqualTo(GameClockBeatStatus.Failed));
            Assert.That(clock.LastError.Code, Is.EqualTo(ApiErrorCodes.NetworkError));
            Assert.That(clock.Current.MinuteOfDay, Is.EqualTo(480));
            Assert.That(clock.IsDue(64.9), Is.False);
            Assert.That(clock.IsDue(65), Is.True);
        }

        [Test]
        public async Task BeatAsync_PostsToGameClockBeatsAndReadsTime()
        {
            var transport = new RecordingTransport();
            var api = new GameClockApi(new ApiClient("", transport));

            GameClockTime time = await api.BeatAsync();

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/me/game-clock/beats"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            Assert.That(time.Day, Is.EqualTo(3));
            Assert.That(time.MinuteOfDay, Is.EqualTo(615));
            Assert.That(time.Paused, Is.True);
        }

        [Test]
        public void Flow_WhileReadingInteractionPause_StandsStillWithoutReleasingTutorialHold()
        {
            var clock = new GameClock { IsLearningWindowOpen = true };
            clock.Flow(30);
            Assert.That(clock.Minutes, Is.EqualTo(480));

            clock.IsHeld = true;
            clock.IsLearningWindowOpen = false;
            clock.Flow(30);
            Assert.That(clock.Minutes, Is.EqualTo(480));
        }

        [Test]
        public async Task InteractionPauseTransitions_SendABeatWithoutWaitingThirtySeconds()
        {
            var api = new FakeGameClockApi { Reply = Time(1, 480) };
            var clock = new GameClock();
            await clock.TickAsync(api, 5);

            clock.IsLearningWindowOpen = true;
            Assert.That(clock.IsDue(6), Is.True);
            await clock.TickAsync(api, 6);
            clock.IsLearningWindowOpen = false;
            Assert.That(clock.IsDue(7), Is.True);
            await clock.TickAsync(api, 7);

            Assert.That(api.Calls, Is.EqualTo(3));
        }

        [Test]
        public async Task BeatAsync_ReportsTheInteractionPauseAndThePausedInterval()
        {
            var transport = new RecordingTransport();
            var api = new GameClockApi(new ApiClient("", transport));

            await api.BeatAsync(interactionPaused: false, interactionWasPaused: true);

            Assert.That(
                transport.Request.JsonBody,
                Does.Match("\"interactionPaused\"\\s*:\\s*false")
            );
            Assert.That(
                transport.Request.JsonBody,
                Does.Match("\"interactionWasPaused\"\\s*:\\s*true")
            );
        }

        [Test]
        public void Minutes_BeforeServerTime_StartsAtEightOnDayOneLikeANewUser()
        {
            var clock = new GameClock();

            Assert.That(clock.Minutes, Is.EqualTo(8 * 60));
        }

        [Test]
        public void Flow_TenRealMinutes_PassesOneGameDay()
        {
            var clock = new GameClock();

            clock.Flow(60);
            Assert.That(clock.Minutes, Is.EqualTo(8 * 60 + 144).Within(.001));

            clock.Flow(540);
            Assert.That(clock.Minutes, Is.EqualTo(32 * 60).Within(.001), "2일차 오전 8시");
        }

        [Test]
        public async Task TickAsync_Updated_MovesFlowingTimeToTheServerTime()
        {
            var api = new FakeGameClockApi { Reply = Time(2, 600) };
            var clock = new GameClock();
            clock.Flow(100);

            await clock.TickAsync(api, 5);
            clock.Flow(10);

            Assert.That(clock.Minutes, Is.EqualTo(24 * 60 + 600 + 24).Within(.001));
        }

        [Test]
        public async Task TickAsync_Updated_KeepsTheTimeThatFlowedWhileWaitingForTheServer()
        {
            // 서버 시각은 요청을 받은 때의 시각이다. 응답을 기다리는 동안 흐른 시간을 버리면 응답이 늦을수록 시계가
            // 뒤로 튀고(실제 2초가 게임 5분), 그 시각에 맞춰 NPC 출퇴근이 한꺼번에 다시 놓인다.
            var api = new FakeGameClockApi { Reply = Time(1, 600) };
            var clock = new GameClock();
            await clock.TickAsync(api, 1);
            api.Gate = new TaskCompletionSource<GameClockTime>();

            Task<GameClockBeatStatus> pending = clock.TickAsync(api, 31);
            clock.Flow(3);
            api.Gate.SetResult(Time(1, 610));
            await pending;

            Assert.That(
                clock.Minutes,
                Is.EqualTo(610 + 3 * GameClock.GameMinutesPerRealSecond).Within(.001)
            );
        }

        [Test]
        public async Task TickAsync_AfterAFailedBeat_SendsItsSecondsWithTheNextBeat()
        {
            // 실패한 신호의 진행 초를 버리면 서버 시각이 그만큼 늦어져, 다음 신호에서 시계가 크게 뒤로 튄다(30초면 게임 72분).
            // 서버가 받았는데 응답만 실패했더라도 서버는 지난 신호부터 실제로 지난 시간까지만 더하므로 두 번 더해지지 않는다.
            var api = new FakeGameClockApi { Reply = Time(1, 480) };
            var clock = new GameClock();
            await clock.TickAsync(api, 1);
            clock.Flow(30);
            api.Failure = new ApiException(ApiErrorCodes.NetworkError, 0, "연결 실패");
            await clock.TickAsync(api, 31);
            api.Failure = null;
            clock.Flow(30);

            await clock.TickAsync(api, 61);

            Assert.That(api.ActiveSeconds, Is.EqualTo(60));
        }

        [Test]
        public async Task Flow_WhileTheServerSaysPaused_StandsStill()
        {
            var api = new FakeGameClockApi
            {
                Reply = new GameClockTime
                {
                    Day = 1,
                    MinuteOfDay = 700,
                    Paused = true,
                },
            };
            var clock = new GameClock();
            await clock.TickAsync(api, 5);

            clock.Flow(10);

            Assert.That(clock.Minutes, Is.EqualTo(700), "대화 중에는 게임 시간이 멈춘다");
        }

        [Test]
        public void Flow_WhileHeld_StandsStill_AndFlowsAgainAfterRelease()
        {
            // 튜토리얼처럼 진행 흐름이 시계를 붙잡아 두면 서버 시각을 받기 전에도 흐르지 않는다.
            var clock = new GameClock { IsHeld = true };

            clock.Flow(60);
            Assert.That(clock.Minutes, Is.EqualTo(8 * 60), "붙잡힌 동안은 1일차 오전 8시에 머문다");

            clock.IsHeld = false;
            clock.Flow(60);
            Assert.That(clock.Minutes, Is.EqualTo(8 * 60 + 144).Within(.001));
        }

        [Test]
        public void Flow_WhileTalking_StopsBeforeAnyServerResponse()
        {
            var clock = new GameClock { IsTalking = true };
            clock.Flow(60);
            Assert.That(clock.Minutes, Is.EqualTo(480));
            clock.IsTalking = false;
            clock.Flow(1);
            Assert.That(clock.Minutes, Is.GreaterThan(480));
        }

        [Test]
        public async Task WindowOpenedAndClosedDuringARequest_DiscardsTheStaleTimeAndFlushesThePause()
        {
            var api = new FakeGameClockApi { Gate = new TaskCompletionSource<GameClockTime>() };
            var clock = new GameClock();
            Task<GameClockBeatStatus> pending = clock.TickAsync(api, 5);
            clock.IsLearningWindowOpen = true;
            clock.IsLearningWindowOpen = false;
            api.Gate.SetResult(Time(1, 500));
            await pending;
            Assert.That(clock.Minutes, Is.EqualTo(480));
            Assert.That(clock.IsDue(6), Is.True);
        }

        [Test]
        public async Task OpeningWindow_ReportsOnlyTheTimeThatActuallyFlowed()
        {
            var api = new FakeGameClockApi { Reply = Time(1, 480) };
            var clock = new GameClock();
            await clock.TickAsync(api, 1);
            clock.Flow(25);
            clock.IsLearningWindowOpen = true;
            await clock.TickAsync(api, 26);
            Assert.That(api.ActiveSeconds, Is.EqualTo(25));
            clock.Flow(60);
            clock.IsLearningWindowOpen = false;
            await clock.TickAsync(api, 86);
            Assert.That(api.ActiveSeconds, Is.Zero);
        }

        [Test]
        public async Task BeatAsync_ReportsActiveSecondsBeforeTheWindowOpened()
        {
            var transport = new RecordingTransport();
            var api = new GameClockApi(new ApiClient("", transport));
            await api.BeatAsync(true, false, 25);
            Assert.That(transport.Request.JsonBody, Does.Match("\"activeSeconds\"\\s*:\\s*25"));
        }

        private static GameClockTime Time(int day, int minuteOfDay)
        {
            return new GameClockTime { Day = day, MinuteOfDay = minuteOfDay };
        }

        private sealed class FakeGameClockApi : IGameClockApi
        {
            public int Calls { get; private set; }
            public long? ActiveSeconds { get; private set; }

            public GameClockTime Reply { get; set; }

            public ApiException Failure { get; set; }

            // 정해 두면 응답을 SetResult할 때까지 붙잡아 둔다.
            public TaskCompletionSource<GameClockTime> Gate { get; set; }

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            )
            {
                Calls++;
                ActiveSeconds = activeSeconds;
                if (Failure != null)
                {
                    return Task.FromException<GameClockTime>(Failure);
                }

                return Gate != null ? Gate.Task : Task.FromResult(Reply);
            }
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(
                    ApiTransportResponse.Received(
                        200,
                        "{\"day\":3,\"minuteOfDay\":615,\"paused\":true}"
                    )
                );
            }
        }
    }
}
