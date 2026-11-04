using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class FirstDayMissionApiTests
    {
        [Test]
        public async Task TownStoryCanBeReadAndStartedThroughItsOwnEndpoints()
        {
            var transport = new RecordingTransport();
            var api = new FirstDayMissionApi(new ApiClient("", transport));

            await api.GetTownStoryAsync();
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/missions/town-story"));
            Assert.That(transport.Request.Method, Is.EqualTo("GET"));

            await api.StartTownStoryAsync();
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/missions/town-story/start"));
            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public async Task CurrentJourneyUsesTownStoryOnlyAfterFirstDayCompletes(
            bool firstDayCompleted,
            bool expectedTownStory
        )
        {
            var transport = new JourneyTransport(firstDayCompleted);
            var api = new FirstDayMissionApi(new ApiClient("", transport));

            FirstDayMissionState state = await api.LoadCurrentAsync("TOWN");

            Assert.That(state.IsTownStory, Is.EqualTo(expectedTownStory));
            Assert.That(transport.TownStoryRequested, Is.EqualTo(expectedTownStory));
        }

        [TestCase(false, "/api/v1/missions/first-day/start")]
        [TestCase(true, "/api/v1/missions/town-story/start")]
        public async Task StartingCurrentJourneyUsesTheSelectedStory(
            bool isTownStory,
            string expected
        )
        {
            var transport = new StartTransport(isTownStory);
            var api = new FirstDayMissionApi(new ApiClient("", transport));

            FirstDayMissionState state = await api.StartCurrentAsync("TOWN", isTownStory);

            Assert.That(transport.StartUrl, Is.EqualTo(expected));
            Assert.That(state.IsTownStory, Is.EqualTo(isTownStory));
        }

        [Test]
        public async Task SceneVisitRestoresServerProgressWithoutSendingClientCompletion()
        {
            var transport = new RecordingTransport();
            var api = new FirstDayMissionApi(new ApiClient("", transport));
            FirstDayMissionState state = await api.VisitAsync("CAFE");
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Stage, Is.EqualTo(3));
            Assert.That(state.Steps[0].Goals[0].Completed, Is.True);
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/missions/first-day/visit"));
            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            string body = transport.Request.JsonBody;
            Assert.That(body, Does.Contain("\"scene\":\"CAFE\""));
            Assert.That(body, Does.Not.Contain("stage"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        private sealed class StartTransport : IApiTransport
        {
            private readonly bool firstDayCompleted;
            public string StartUrl { get; private set; }

            public StartTransport(bool firstDayCompleted) =>
                this.firstDayCompleted = firstDayCompleted;

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                if (request.Url.EndsWith("/start"))
                    StartUrl = request.Url;
                bool completed =
                    firstDayCompleted && request.Url == "/api/v1/missions/first-day/visit";
                string body =
                    $"{{\"started\":true,\"completed\":{completed.ToString().ToLowerInvariant()},\"steps\":[]}}";
                return Task.FromResult(ApiTransportResponse.Received(200, body));
            }
        }

        private sealed class JourneyTransport : IApiTransport
        {
            private readonly bool firstDayCompleted;
            public bool TownStoryRequested { get; private set; }

            public JourneyTransport(bool firstDayCompleted) =>
                this.firstDayCompleted = firstDayCompleted;

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                TownStoryRequested = request.Url == "/api/v1/missions/town-story";
                string body = TownStoryRequested
                    ? "{\"started\":false,\"completed\":false,\"steps\":[]}"
                    : $"{{\"started\":true,\"completed\":{firstDayCompleted.ToString().ToLowerInvariant()},\"steps\":[]}}";
                return Task.FromResult(ApiTransportResponse.Received(200, body));
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
                        "{\"started\":true,\"stage\":3,\"steps\":[{\"goals\":[{\"completed\":true}]}]}"
                    )
                );
            }
        }
    }
}
