using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class FirstDayMissionApiTests
    {
        [Test]
        public async Task ShowcaseUsesSeparateAuthenticatedRoutesAndExplicitArrivalDestination()
        {
            var transport = new RecordingTransport();
            var api = new FirstDayMissionApi(new ApiClient("", transport));
            await api.StartShowcaseAsync();
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/missions/showcase/start"));
            await api.ConfirmShowcaseTaxiArrivalAsync("DINER");
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/missions/showcase/taxi-arrival")
            );
            Assert.That(transport.Request.JsonBody, Does.Contain("\"destination\":\"DINER\""));
            Assert.That(transport.Request.JsonBody, Does.Not.Contain("stage"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            await api.StopShowcaseAsync();
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/missions/showcase/stop"));
        }

        [Test]
        public async Task CancellingAQuest_PostsToItsCancelRouteAndReadsTheNewBoardWithLockReasons()
        {
            var transport = new BodyTransport(
                "{\"balance\":100,\"quests\":[{\"id\":\"CHECK_IN\",\"status\":\"AVAILABLE\",\"lockReason\":null},"
                    + "{\"id\":\"MORNING_AND_HAIRCUT\",\"status\":\"LOCKED\",\"lockReason\":\"'호텔 체크인과 잃어버린 가방' 퀘스트를 마치면 열려요.\"}]}"
            );
            var api = new FirstDayMissionApi(new ApiClient("", transport));

            QuestBoardState board = await api.CancelQuestAsync("CHECK_IN");

            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/missions/quests/CHECK_IN/cancel")
            );
            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            Assert.That(board.Quests[0].Status, Is.EqualTo(QuestEntry.Available));
            Assert.That(
                board.Quests[1].LockReason,
                Is.EqualTo("'호텔 체크인과 잃어버린 가방' 퀘스트를 마치면 열려요.")
            );
        }

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

        [Test]
        public async Task PracticePlaces_AreReadWithTheirScenariosAndGoals()
        {
            var transport = new BodyTransport(
                "{\"places\":[{\"placeCode\":\"DINER\",\"name\":\"식당\","
                    + "\"summary\":\"자리 잡기 · 못 먹는 재료 말하기\",\"npcCode\":\"DINER_STAFF\","
                    + "\"npcName\":\"Rosa\",\"roleLabel\":\"식당 직원\",\"sceneCode\":\"TOWN\","
                    + "\"doneCount\":1,\"totalCount\":3,\"scenarios\":[{\"scenarioId\":\"DINER_TABLE\","
                    + "\"number\":1,\"title\":\"자리 잡기\",\"description\":\"창가 자리를 부탁해요.\","
                    + "\"done\":true,\"goals\":[{\"goalId\":\"say_party_window\","
                    + "\"title\":\"인원 말하고 창가 자리 부탁하기\",\"hint\":\"Two, please.\"}]}]}]}"
            );
            var api = new FirstDayMissionApi(new ApiClient("", transport));

            PracticeBoardState board = await api.GetPracticeBoardAsync();

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/practice-places"));
            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            PracticePlace diner = board.Places[0];
            Assert.That(diner.PlaceCode, Is.EqualTo("DINER"));
            Assert.That(diner.Summary, Is.EqualTo("자리 잡기 · 못 먹는 재료 말하기"));
            Assert.That(diner.NpcCode, Is.EqualTo("DINER_STAFF"));
            Assert.That(diner.NpcName, Is.EqualTo("Rosa"));
            Assert.That(diner.RoleLabel, Is.EqualTo("식당 직원"));
            Assert.That(diner.SceneCode, Is.EqualTo("TOWN"));
            Assert.That(diner.DoneCount, Is.EqualTo(1));
            Assert.That(diner.TotalCount, Is.EqualTo(3));
            PracticeScenario table = diner.Scenarios[0];
            Assert.That(table.ScenarioId, Is.EqualTo("DINER_TABLE"));
            Assert.That(table.Number, Is.EqualTo(1));
            Assert.That(table.Title, Is.EqualTo("자리 잡기"));
            Assert.That(table.Done, Is.True);
            Assert.That(table.Goals[0].GoalId, Is.EqualTo("say_party_window"));
            Assert.That(table.Goals[0].Title, Is.EqualTo("인원 말하고 창가 자리 부탁하기"));
            Assert.That(table.Goals[0].Hint, Is.EqualTo("Two, please."));
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

        private sealed class BodyTransport : IApiTransport
        {
            private readonly string body;

            public BodyTransport(string body) => this.body = body;

            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
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
