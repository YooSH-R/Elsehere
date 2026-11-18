using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    /// <summary>돌발 부탁의 서버 응답을 읽는다. 턴 응답(CONV-03)의 missionUpdates, 진행 중 부탁(FAV-01), 그만두기(FAV-02).</summary>
    public class FavorApiTests
    {
        private const string Croissant =
            "{\"favorId\":42,\"templateId\":\"bring_food\",\"status\":\"ACCEPTED\","
            + "\"requesterNpcCode\":\"HOTEL_STAFF\",\"titleKo\":\"Jake의 부탁 · 크루아상 사다 주기\","
            + "\"steps\":[{\"placeCode\":\"BAKERY\",\"npcCode\":null,\"textKo\":\"Maple Bakery에서 크루아상 포장 주문하기\",\"done\":false},"
            + "{\"placeCode\":\"HOTEL\",\"npcCode\":\"HOTEL_STAFF\",\"textKo\":\"호텔 Jake에게 건네기\",\"done\":false}],"
            + "\"rewardCoins\":6,\"affinityDelta\":8,\"fills\":null,\"expiresAtGameMinute\":4380}";

        [Test]
        public async Task GetCurrentAsync_ReadsTheFavorInProgress()
        {
            var transport = new RecordingTransport { ReplyBody = Croissant };

            FavorUpdate favor = await new FavorApi(new ApiClient("", transport)).GetCurrentAsync();

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/favors/current"));
            Assert.That(favor.FavorId, Is.EqualTo(42));
            Assert.That(favor.IsActive, Is.True);
            Assert.That(favor.RequesterNpcCode, Is.EqualTo("HOTEL_STAFF"));
            Assert.That(favor.Steps, Has.Count.EqualTo(2));
            Assert.That(favor.Steps[0].PlaceCode, Is.EqualTo("BAKERY"));
            Assert.That(favor.Steps[1].NpcCode, Is.EqualTo("HOTEL_STAFF"));
            Assert.That(favor.ExpiresAtGameMinute, Is.EqualTo(4380));
            Assert.That(favor.RequesterLabel, Is.EqualTo("Jake의 부탁"));
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task GetCurrentAsync_ReadsNoContentAsNoFavor(string body)
        {
            var transport = new RecordingTransport { StatusCode = 204, ReplyBody = body };

            FavorUpdate favor = await new FavorApi(new ApiClient("", transport)).GetCurrentAsync();

            Assert.That(favor, Is.Null);
        }

        [Test]
        public async Task AbandonAsync_PostsToTheFavor()
        {
            var transport = new RecordingTransport
            {
                ReplyBody = Croissant.Replace("ACCEPTED", "ABANDONED"),
            };

            FavorUpdate favor = await new FavorApi(new ApiClient("", transport)).AbandonAsync(42);

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/favors/42/abandon"));
            Assert.That(favor.IsActive, Is.False);
        }

        [Test]
        public async Task CallAsync_SendsNearbyNpcsAndReadsTheFavorTheyAsk()
        {
            var transport = new RecordingTransport
            {
                ReplyBody = Croissant.Replace("ACCEPTED", "OFFERED"),
            };
            var candidates = new[]
            {
                new FavorCallCandidate
                {
                    NpcCode = "HOTEL_STAFF",
                    Needs = new Dictionary<string, float> { ["HUNGER"] = 20f },
                },
            };

            FavorUpdate favor = await new FavorApi(new ApiClient("", transport)).CallAsync(
                candidates
            );

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/favors/calls"));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"npcCode\":\"HOTEL_STAFF\""));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"HUNGER\":20.0"));
            Assert.That(favor.Status, Is.EqualTo(FavorUpdate.Offered));
        }

        [Test]
        public async Task CallAsync_ReadsNoContentAsNoCall()
        {
            var transport = new RecordingTransport { StatusCode = 204, ReplyBody = "" };

            FavorUpdate favor = await new FavorApi(new ApiClient("", transport)).CallAsync(
                new FavorCallCandidate[0]
            );

            Assert.That(favor, Is.Null);
        }

        [Test]
        public async Task DeclineAsync_PostsToTheFavor()
        {
            var transport = new RecordingTransport
            {
                ReplyBody = Croissant.Replace("ACCEPTED", "DECLINED"),
            };

            await new FavorApi(new ApiClient("", transport)).DeclineAsync(42);

            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/favors/42/decline"));
        }

        [Test]
        public async Task SendTurnAsync_ReadsTheFavorUpdatesOfTheTurn()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"npcTurn\":{\"turnId\":11,\"text\":\"Thanks!\"},\"missionUpdates\":["
                    + Croissant
                        .Replace("ACCEPTED", "DONE")
                        .Replace("\"fills\":null", "\"fills\":{\"HUNGER\":40}")
                    + "]}",
            };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );

            TurnResult turn = await api.SendTurnAsync(
                58,
                "request-2",
                "TEXT",
                "Here you go.",
                null,
                null
            );

            Assert.That(turn.MissionUpdates, Has.Count.EqualTo(1));
            Assert.That(turn.MissionUpdates[0].IsDone, Is.True);
            Assert.That(turn.MissionUpdates[0].Fills["HUNGER"], Is.EqualTo(40));
            Assert.That(turn.MissionUpdates[0].Notice, Is.EqualTo("Jake의 부탁을 들어줬어요"));
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public int StatusCode { get; set; } = 200;
            public string ReplyBody { get; set; } = "{}";

            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(ApiTransportResponse.Received(StatusCode, ReplyBody));
            }
        }
    }
}
