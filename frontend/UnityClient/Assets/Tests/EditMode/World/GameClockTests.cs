using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.World.Tests
{
    public class GameClockTests
    {
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

        private static GameClockTime Time(int day, int minuteOfDay)
        {
            return new GameClockTime { Day = day, MinuteOfDay = minuteOfDay };
        }

        private sealed class FakeGameClockApi : IGameClockApi
        {
            public int Calls { get; private set; }

            public GameClockTime Reply { get; set; }

            public ApiException Failure { get; set; }

            // 정해 두면 응답을 SetResult할 때까지 붙잡아 둔다.
            public TaskCompletionSource<GameClockTime> Gate { get; set; }

            public Task<GameClockTime> BeatAsync()
            {
                Calls++;
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
