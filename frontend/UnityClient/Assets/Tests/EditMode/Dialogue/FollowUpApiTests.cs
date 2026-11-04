using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    /// <summary>
    /// NPC가 준비해 건네기로 한 것의 서버 응답을 읽는다. 대화 종료(CONV-08)의 followUp, 목록(CONV-13)의 items,
    /// 대화 시작(CONV-01)의 deliveredFollowUpId.
    /// </summary>
    public class FollowUpApiTests
    {
        private const string IcedLatte =
            "{\"followUpId\":7,\"npc\":{\"npcCode\":\"CAFE_STAFF\",\"name\":\"Mia\",\"roleLabel\":\"카페 직원\"},"
            + "\"sceneCode\":\"CAFE\",\"item\":\"iced latte\",\"itemKo\":\"아이스 라테\","
            + "\"readyAt\":\"2026-11-04T14:05:45+09:00\",\"remainingSeconds\":45,\"ready\":false}";

        [Test]
        public async Task GetFollowUpsAsync_ReadsEveryFollowUpNotYetHandedOver()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"items\":["
                    + IcedLatte
                    + ",{\"followUpId\":8,\"npc\":{\"npcCode\":\"DINER_STAFF\",\"name\":\"Rosa\"},"
                    + "\"sceneCode\":\"TOWN\",\"item\":\"breakfast\",\"itemKo\":\"아침 식사\","
                    + "\"readyAt\":\"2026-11-04T14:04:00+09:00\",\"remainingSeconds\":0,\"ready\":true}]}",
            };
            var api = new FollowUpApi(new ApiClient("", transport));

            FollowUpList list = await api.GetFollowUpsAsync();

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/follow-ups"));
            Assert.That(list.Items, Has.Count.EqualTo(2));
            FollowUpData latte = list.Items[0];
            Assert.That(latte.FollowUpId, Is.EqualTo(7));
            Assert.That(latte.Npc.NpcCode, Is.EqualTo("CAFE_STAFF"));
            Assert.That(latte.Npc.Name, Is.EqualTo("Mia"));
            Assert.That(latte.SceneCode, Is.EqualTo("CAFE"));
            Assert.That(latte.Item, Is.EqualTo("iced latte"));
            Assert.That(latte.ItemKo, Is.EqualTo("아이스 라테"));
            Assert.That(latte.RemainingSeconds, Is.EqualTo(45));
            Assert.That(latte.Ready, Is.False);
            Assert.That(list.Items[1].Ready, Is.True);
            Assert.That(list.Items[1].RemainingSeconds, Is.EqualTo(0));
        }

        [Test]
        public async Task EndAsync_ReadsWhatTheNpcPromisedToHandOver()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"status\":\"ENDED_BY_USER\",\"feedback\":{\"status\":\"DONE\"},\"followUp\":"
                    + IcedLatte
                    + "}",
            };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );

            EndResult end = await api.EndAsync(58, "end-1", false);

            Assert.That(end.FollowUp.FollowUpId, Is.EqualTo(7));
            Assert.That(end.FollowUp.ItemKo, Is.EqualTo("아이스 라테"));
            Assert.That(end.FollowUp.RemainingSeconds, Is.EqualTo(45));
        }

        [Test]
        public async Task EndAsync_WithNothingOwed_HasNoFollowUp()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"status\":\"ENDED_BY_USER\",\"feedback\":{\"status\":\"DONE\"},\"followUp\":null}",
            };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );

            EndResult end = await api.EndAsync(58, "end-1", false);

            Assert.That(end.FollowUp, Is.Null);
        }

        [TestCase("7", 7L)]
        [TestCase("null", null)]
        public async Task StartAsync_ReadsWhichFollowUpTheOpeningLineHandsOver(
            string delivered,
            long? expected
        )
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"conversationId\":58,\"npc\":{\"npcCode\":\"CAFE_STAFF\",\"name\":\"Mia\"},"
                    + "\"openingTurn\":{\"turnId\":101,\"text\":\"Here's your iced latte!\"},"
                    + "\"deliveredFollowUpId\":"
                    + delivered
                    + "}",
            };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );

            ConversationStart start = await api.StartAsync(
                "start-1",
                "CAFE_STAFF",
                "NPC",
                npcOffDuty: false
            );

            Assert.That(start.DeliveredFollowUpId, Is.EqualTo(expected));
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
