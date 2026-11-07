using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ReviewApiTests
    {
        private RecordingTransport transport;
        private ReviewApi api;

        [SetUp]
        public void SetUp()
        {
            transport = new RecordingTransport();
            api = new ReviewApi(new ApiClient("", transport));
        }

        [Test]
        public async Task GetPage_ReadsSavedSituationAndExpressionsWithoutConversationCalls()
        {
            transport.Body =
                "{\"items\":[{\"id\":42,\"sourceConversationId\":55,\"sceneCode\":\"CAFE\","
                + "\"npc\":{\"npcCode\":\"CAFE_STAFF\",\"name\":\"Mia\"},"
                + "\"situationSummary\":\"포장을 부탁했어요.\",\"targets\":[{\"expression\":\"Can I get that to go?\","
                + "\"meaningKo\":\"포장해 주실 수 있나요?\",\"usageKo\":\"가지고 나갈 때\"}],"
                + "\"practiceCount\":2,\"lastPracticedAt\":\"2026-11-06T07:00:00Z\","
                + "\"usedExpressions\":[\"Can I get that to go?\"]}],\"nextCursor\":\"42\"}";

            ReviewPage page = await api.GetPageAsync("CAFE", null);

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/review-cards?size=20&sceneCode=CAFE")
            );
            Assert.That(page.NextCursor, Is.EqualTo("42"));
            Assert.That(page.Items[0].Npc.Name, Is.EqualTo("Mia"));
            Assert.That(page.Items[0].SourceConversationId, Is.EqualTo(55));
            Assert.That(page.Items[0].Targets[0].MeaningKo, Is.EqualTo("포장해 주실 수 있나요?"));
            Assert.That(page.Items[0].PracticeCount, Is.EqualTo(2));
            Assert.That(page.Items[0].LastPracticedAt.Value.Hour, Is.EqualTo(7));
        }

        [Test]
        public async Task GetPage_EscapesCursorAndDoesNotSendAnEmptySceneFilter()
        {
            transport.Body = "{\"items\":[],\"nextCursor\":null}";

            await api.GetPageAsync(null, "42&size=99");

            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/review-cards?size=20&cursor=42%26size%3D99")
            );
        }

        [Test]
        public async Task Save_SendsOnlyTheOriginalConversationAndUsesTheWriteHeader()
        {
            transport.Body = "{\"id\":42,\"sourceConversationId\":55}";

            ReviewCard saved = await api.SaveAsync(55);

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/review-cards"));
            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"conversationId\":55}"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            Assert.That(saved.Id, Is.EqualTo(42));
        }

        [Test]
        public async Task Start_UsesTheDedicatedReviewRouteAndKeepsTheRequestId()
        {
            transport.Body = "{\"conversationId\":88,\"reviewCardId\":42}";

            var started = await api.StartAsync(42, "test-request");

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/review-cards/42/conversations"));
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"requestId\":\"test-request\"}"));
            Assert.That(started.ConversationId, Is.EqualTo(88));
        }

        [Test]
        public async Task Delete_DeletesOnlyTheSelectedCard()
        {
            await api.DeleteAsync(42);

            Assert.That(transport.Request.Method, Is.EqualTo("DELETE"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/review-cards/42"));
        }

        [Test]
        public async Task GetResult_ReadsSavedEvidenceWithoutStartingAnotherConversation()
        {
            transport.Body =
                "{\"conversationId\":88,\"reviewCardId\":42,\"practiceCompleted\":true,\"verifiedExpressionCount\":2,\"expressions\":[{\"expression\":\"Can I get that to go?\",\"evidence\":[{\"turnId\":91,\"text\":\"Could I get that to go, please?\"}]}]}";
            var result = await api.GetResultAsync(42);
            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/review-cards/42/result"));
            Assert.That(result.VerifiedExpressionCount, Is.EqualTo(2));
            Assert.That(
                result.Expressions[0].Evidence[0].Text,
                Is.EqualTo("Could I get that to go, please?")
            );
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
