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
