using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class PracticeApiTests
    {
        private RecordingTransport transport;
        private PracticeApi api;

        [SetUp]
        public void SetUp()
        {
            transport = new RecordingTransport();
            api = new PracticeApi(new ApiClient("", transport));
        }

        [Test]
        public async Task Start_UsesTheScenarioRouteAndKeepsTheRequestId()
        {
            transport.Body = "{\"conversationId\":88,\"practiceScenarioId\":\"DINER_TABLE\"}";

            ConversationStart started = await api.StartAsync("DINER_TABLE", "test-request");

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/practice-scenarios/DINER_TABLE/conversations")
            );
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"requestId\":\"test-request\"}"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            Assert.That(started.ConversationId, Is.EqualTo(88));
            Assert.That(started.PracticeScenarioId, Is.EqualTo("DINER_TABLE"));
        }

        [Test]
        public async Task GetProgress_ReadsWhichGoalsTheServerRecognised()
        {
            transport.Body =
                "{\"scenarioId\":\"DINER_TABLE\",\"done\":false,\"goals\":["
                + "{\"goalId\":\"say_party_window\",\"title\":\"인원 말하고 창가 자리 부탁하기\","
                + "\"hint\":\"Two, please.\",\"completed\":true},"
                + "{\"goalId\":\"ask_wait\",\"title\":\"기다리는 시간 묻기\","
                + "\"hint\":\"How long is the wait?\",\"completed\":false}]}";

            PracticeProgress progress = await api.GetProgressAsync("DINER_TABLE", 88);

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/practice-scenarios/DINER_TABLE/conversations/88/progress")
            );
            Assert.That(progress.ScenarioId, Is.EqualTo("DINER_TABLE"));
            Assert.That(progress.Done, Is.False);
            Assert.That(progress.Goals[0].GoalId, Is.EqualTo("say_party_window"));
            Assert.That(progress.Goals[0].Title, Is.EqualTo("인원 말하고 창가 자리 부탁하기"));
            Assert.That(progress.Goals[0].Hint, Is.EqualTo("Two, please."));
            Assert.That(progress.Goals[0].Completed, Is.True);
            Assert.That(progress.Goals[1].Completed, Is.False);
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public string Body = "{}";
            public ApiTransportRequest Request;

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(ApiTransportResponse.Received(200, Body));
            }
        }
    }
}
