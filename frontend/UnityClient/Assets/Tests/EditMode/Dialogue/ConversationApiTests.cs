using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class ConversationApiTests
    {
        [Test]
        public async Task PerformanceIntent_SendsOnlyTextToDedicatedEndpoint()
        {
            var transport = new RecordingTransport { ReplyBody = "{\"intent\":\"REPLAY_RAP\"}" };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );
            var result = await api.ClassifyPerformanceAsync(58, "intent-1", "An encore?");
            Assert.That(result.Intent, Is.EqualTo("REPLAY_RAP"));
            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/conversations/58/performance-intent")
            );
            Assert.That(
                transport.Request.JsonBody,
                Is.EqualTo("{\"requestId\":\"intent-1\",\"text\":\"An encore?\"}")
            );
        }

        [Test]
        public async Task StartAsync_SendsActualJourneyFacts()
        {
            var transport = new RecordingTransport();
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );
            await api.StartAsync(
                "request",
                "CAFE_STAFF",
                "USER",
                true,
                new NpcTravelContext
                {
                    Phase = "GOING_TO_WORK",
                    RouteMeters = 80,
                    WalkedMeters = 35,
                    DepartureLeadMinutes = 190,
                    LateMinutes = 0,
                }
            );
            Assert.That(
                transport.Request.JsonBody,
                Does.Contain("\"npcTravelContext\":{")
                    .And.Contain("\"phase\":\"GOING_TO_WORK\"")
                    .And.Contain("\"walkedMeters\":35")
                    .And.Contain("\"departureLeadMinutes\":190")
            );
        }

        [TestCase(true, "\"npcOffDuty\":true")]
        [TestCase(false, "\"npcOffDuty\":false")]
        public async Task StartAsync_SendsWhetherTheNpcIsOffDuty(bool npcOffDuty, string expected)
        {
            var transport = new RecordingTransport
            {
                ReplyBody = "{\"conversationId\":58,\"npc\":{\"npcCode\":\"CAFE_STAFF\"}}",
            };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );

            await api.StartAsync("request-1", "CAFE_STAFF", "USER", npcOffDuty);

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/conversations"));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"npcCode\":\"CAFE_STAFF\""));
            Assert.That(transport.Request.JsonBody, Does.Contain(expected));
        }

        [Test]
        public async Task StartAsync_SendsTheChosenNpcSpeechSpeed()
        {
            using var speed = new ChosenSpeechSpeed(1.3f);
            var transport = new RecordingTransport();

            await CreateApi(transport).StartAsync("request-1", "CAFE_STAFF", "USER", false);

            Assert.That(transport.Request.JsonBody, Does.Contain("\"speechSpeed\":1.3"));
        }

        [Test]
        public async Task SendTurnAsync_SendsTheChosenNpcSpeechSpeed()
        {
            using var speed = new ChosenSpeechSpeed(0.9f);
            var transport = new RecordingTransport();

            await CreateApi(transport).SendTurnAsync(58, "request-2", "TEXT", "Hi", null, null);

            Assert.That(transport.Request.JsonBody, Does.Contain("\"speechSpeed\":0.9"));
        }

        [Test]
        public async Task RegenerateSpeechAsync_SendsTheChosenNpcSpeechSpeed()
        {
            using var speed = new ChosenSpeechSpeed(1.5f);
            var transport = new RecordingTransport();

            await CreateApi(transport).RegenerateSpeechAsync(12, "request-3");

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/turns/12/speech"));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"speechSpeed\":1.5"));
        }

        private static ConversationApi CreateApi(RecordingTransport transport)
        {
            return new ConversationApi(new ApiClient("", transport), new ApiClient("", transport));
        }

        // 설정창에서 고른 값을 테스트 동안만 바꾸고 끝나면 되돌린다.
        private sealed class ChosenSpeechSpeed : System.IDisposable
        {
            private readonly float saved = NpcSpeechSpeed.Current;

            public ChosenSpeechSpeed(float value)
            {
                NpcSpeechSpeed.Current = value;
            }

            public void Dispose()
            {
                NpcSpeechSpeed.Current = saved;
            }
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public string ReplyBody { get; set; } = "{}";

            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(ApiTransportResponse.Received(200, ReplyBody));
            }
        }
    }
}
