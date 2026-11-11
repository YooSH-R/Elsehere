using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ReviewApiTests
    {
        private RecordingTransport transport;
        private ReviewApi api;

        [Test]
        public async Task GetProgress_ReadsTheOwnedAttemptWithoutCreatingOrEndingConversation()
        {
            transport.Body =
                "{\"conversationId\":77,\"reviewCardId\":42,\"verifiedExpressionCount\":1,\"expressions\":[]}";
            ReviewPracticeProgress result = await api.GetProgressAsync(42, 77);
            Assert.That(transport.Request, Is.Not.Null, "진행 조회 요청이 전송되어야 합니다.");
            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/review-cards/42/conversations/77/progress")
            );
            Assert.That(result.VerifiedExpressionCount, Is.EqualTo(1));
        }

        [SetUp]
        public void SetUp()
        {
            transport = new RecordingTransport();
            api = new ReviewApi(new ApiClient("", transport));
        }

        [Test]
        public async Task Save_ReadsTheSavedSituationAndExpressions()
        {
            transport.Body =
                "{\"id\":42,\"sourceConversationId\":55,\"sceneCode\":\"CAFE\","
                + "\"npc\":{\"npcCode\":\"CAFE_STAFF\",\"name\":\"Mia\"},"
                + "\"situationSummary\":\"포장을 부탁했어요.\",\"targets\":[{\"expression\":\"Can I get that to go?\","
                + "\"meaningKo\":\"포장해 주실 수 있나요?\",\"usageKo\":\"가지고 나갈 때\"}],"
                + "\"practiceCount\":2,\"lastPracticedAt\":\"2026-11-06T07:00:00Z\","
                + "\"usedExpressions\":[\"Can I get that to go?\"]}";

            ReviewCard card = await api.SaveAsync(55);

            Assert.That(card.Npc.Name, Is.EqualTo("Mia"));
            Assert.That(card.SourceConversationId, Is.EqualTo(55));
            Assert.That(card.Targets[0].MeaningKo, Is.EqualTo("포장해 주실 수 있나요?"));
            Assert.That(card.PracticeCount, Is.EqualTo(2));
            Assert.That(card.LastPracticedAt.Value.Hour, Is.EqualTo(7));
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

            float saved = NpcSpeechSpeed.Current;
            NpcSpeechSpeed.Current = 1.2f;
            ConversationStart started;
            try
            {
                started = await api.StartAsync(42, "test-request");
            }
            finally
            {
                NpcSpeechSpeed.Current = saved;
            }

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/review-cards/42/conversations"));
            Assert.That(
                transport.Request.JsonBody,
                Is.EqualTo("{\"requestId\":\"test-request\",\"speechSpeed\":1.2}")
            );
            Assert.That(started.ConversationId, Is.EqualTo(88));
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
