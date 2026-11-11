using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ConversationHistoryApiTests
    {
        private const string PageJson =
            "{\"items\":[{\"conversationId\":58,"
            + "\"npc\":{\"npcCode\":\"CAFE_STAFF\",\"name\":\"Mia\",\"roleLabel\":\"카페 직원\"},"
            + "\"sceneCode\":\"CAFE\",\"status\":\"ENDED_BY_USER\",\"endReason\":\"USER_ENDED\","
            + "\"startedAt\":\"2026-10-21T14:00:00+09:00\",\"endedAt\":\"2026-10-21T14:05:00+09:00\","
            + "\"userTurnCount\":4,\"feedbackStatus\":\"DONE\"}],\"nextCursor\":\"58\"}";

        private RecordingTransport transport;
        private ConversationHistoryApi api;

        [SetUp]
        public void CreateApi()
        {
            transport = new RecordingTransport();
            api = new ConversationHistoryApi(new ApiClient("", transport));
        }

        [Test]
        public async Task GetPageAsync_FirstPage_ReadsRecentConversations()
        {
            transport.ReplyBody = PageJson;

            ConversationPage page = await api.GetPageAsync(null, null);

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/conversations?size=20&feedbackOnly=true")
            );
            ConversationSummary item = page.Items[0];
            Assert.That(item.ConversationId, Is.EqualTo(58));
            Assert.That(item.Npc.Name, Is.EqualTo("Mia"));
            Assert.That(item.Npc.RoleLabel, Is.EqualTo("카페 직원"));
            Assert.That(item.SceneCode, Is.EqualTo("CAFE"));
            Assert.That(item.Status, Is.EqualTo("ENDED_BY_USER"));
            Assert.That(item.StartedAt.Hour, Is.EqualTo(14));
            Assert.That(item.UserTurnCount, Is.EqualTo(4));
            Assert.That(item.FeedbackStatus, Is.EqualTo("DONE"));
            Assert.That(page.NextCursor, Is.EqualTo("58"));
        }

        [Test]
        public async Task GetPageAsync_NextPage_SendsCursor()
        {
            transport.ReplyBody = PageJson;

            await api.GetPageAsync("58", null);

            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/conversations?size=20&feedbackOnly=true&cursor=58")
            );
        }

        // 대화 노트의 장소 탭은 그 장소의 대화만 받고, 줄마다 그 대화로 연습한 횟수를 보인다.
        [Test]
        public async Task GetPageAsync_ForAPlace_SendsTheSceneAndReadsPracticeCount()
        {
            transport.ReplyBody = PageJson.Replace(
                "\"feedbackStatus\":\"DONE\"",
                "\"feedbackStatus\":\"DONE\",\"practiceCount\":2"
            );

            ConversationPage page = await api.GetPageAsync("58", "CAFE");

            Assert.That(
                transport.Request.Url,
                Is.EqualTo(
                    "/api/v1/conversations?size=20&feedbackOnly=true&sceneCode=CAFE&cursor=58"
                )
            );
            Assert.That(page.Items[0].PracticeCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GetFeedbackAsync_ReadsSavedFeedbackAndDetails()
        {
            transport.ReplyBody =
                "{\"conversationId\":58,\"npc\":{\"name\":\"Mia\"},\"sceneCode\":\"CAFE\",\"userTurnCount\":4,"
                + "\"feedback\":{\"status\":\"DONE\",\"summary\":\"주문을 끝까지 해냈어요.\"},\"completedMissions\":[],"
                + "\"detailedFeedback\":[{\"turnFeedbackId\":33,\"userText\":\"I want 아이스 라떼.\","
                + "\"betterText\":\"I'd like an iced latte, please.\",\"reason\":\"더 공손해요.\"}]}";

            PastFeedback saved = await api.GetFeedbackAsync(58);

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/conversations/58/feedback"));
            Assert.That(saved.Feedback.Status, Is.EqualTo("DONE"));
            Assert.That(saved.Feedback.Summary, Is.EqualTo("주문을 끝까지 해냈어요."));
            Assert.That(
                saved.DetailedFeedback[0].BetterText,
                Is.EqualTo("I'd like an iced latte, please.")
            );
        }

        [Test]
        public async Task GetDetailAsync_ReadsTurnsInOrder()
        {
            transport.ReplyBody =
                "{\"conversationId\":58,\"status\":\"ENDED_BY_USER\",\"turns\":["
                + "{\"turnId\":101,\"seq\":1,\"speaker\":\"NPC\",\"text\":\"Hi! What can I get you?\","
                + "\"meaningKo\":\"안녕하세요! 뭐 드릴까요?\"},"
                + "{\"turnId\":102,\"seq\":2,\"speaker\":\"USER\",\"text\":\"An iced latte, please.\"}]}";

            ConversationDetail detail = await api.GetDetailAsync(58);

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/conversations/58"));
            Assert.That(detail.Turns[0].Speaker, Is.EqualTo("NPC"));
            Assert.That(detail.Turns[0].MeaningKo, Is.EqualTo("안녕하세요! 뭐 드릴까요?"));
            Assert.That(detail.Turns[1].Speaker, Is.EqualTo("USER"));
            Assert.That(detail.Turns[1].Text, Is.EqualTo("An iced latte, please."));
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
