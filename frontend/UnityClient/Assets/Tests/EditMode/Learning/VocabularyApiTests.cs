using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class VocabularyApiTests
    {
        private const string PageJson =
            "{\"counts\":{\"all\":12},\"items\":[{\"userVocabId\":21,"
            + "\"vocab\":{\"vocabId\":77,\"type\":\"WORD\",\"text\":\"latte\",\"meaningKo\":\"라떼\"},"
            + "\"context\":{\"text\":\"Would you like an iced latte?\",\"meaningKo\":\"아이스 라떼 드릴까요?\"},"
            + "\"memo\":\"주문할 때 쓰기\",\"memorized\":true,\"savedAt\":\"2026-10-21T14:06:00+09:00\","
            + "\"source\":{\"sourceType\":\"NPC_LINE\",\"npcName\":\"Mia\",\"conversationId\":58}}],"
            + "\"nextCursor\":\"21\"}";

        private const string ItemJson =
            "{\"userVocabId\":21,\"vocab\":{\"vocabId\":77,\"text\":\"latte\",\"meaningKo\":\"라떼\"},"
            + "\"context\":null,\"memo\":\"주문할 때 쓰기\",\"memorized\":false,"
            + "\"savedAt\":\"2026-10-21T14:06:00+09:00\",\"source\":{\"sourceType\":\"OBJECT\"}}";

        private RecordingTransport transport;
        private VocabularyApi api;

        [SetUp]
        public void CreateApi()
        {
            transport = new RecordingTransport();
            api = new VocabularyApi(new ApiClient("", transport));
        }

        [Test]
        public async Task GetPageAsync_FirstPage_ReadsMemorizedWordsToo()
        {
            transport.ReplyBody = PageJson;

            VocabPage page = await api.GetPageAsync(null);

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/vocab?includeMemorized=true&size=50")
            );
            Assert.That(page.Counts.All, Is.EqualTo(12));
            Assert.That(page.NextCursor, Is.EqualTo("21"));
            VocabItem item = page.Items[0];
            Assert.That(item.UserVocabId, Is.EqualTo(21));
            Assert.That(item.Vocab.Text, Is.EqualTo("latte"));
            Assert.That(item.Vocab.MeaningKo, Is.EqualTo("라떼"));
            Assert.That(item.Context.Text, Is.EqualTo("Would you like an iced latte?"));
            Assert.That(item.Memo, Is.EqualTo("주문할 때 쓰기"));
            Assert.That(item.Memorized, Is.True);
            Assert.That(item.Source.NpcName, Is.EqualTo("Mia"));
        }

        [Test]
        public async Task GetPageAsync_NextPage_SendsCursor()
        {
            transport.ReplyBody = PageJson;

            await api.GetPageAsync("21");

            Assert.That(
                transport.Request.Url,
                Is.EqualTo("/api/v1/vocab?includeMemorized=true&size=50&cursor=21")
            );
        }

        [Test]
        public async Task UpdateMemoAsync_PatchesOnlyMemo()
        {
            transport.ReplyBody = ItemJson;

            VocabItem item = await api.UpdateMemoAsync(21, "주문할 때 쓰기");

            Assert.That(transport.Request.Method, Is.EqualTo("PATCH"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/vocab/21"));
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"memo\":\"주문할 때 쓰기\"}"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            Assert.That(item.Memo, Is.EqualTo("주문할 때 쓰기"));
            Assert.That(item.Context, Is.Null);
        }

        [Test]
        public async Task UpdateMemorizedAsync_PatchesOnlyMemorized()
        {
            transport.ReplyBody = ItemJson;

            await api.UpdateMemorizedAsync(21, true);

            Assert.That(transport.Request.Method, Is.EqualTo("PATCH"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/vocab/21"));
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"memorized\":true}"));
        }

        [Test]
        public async Task DeleteAsync_DeletesEntry()
        {
            transport.ReplyStatus = 204;
            transport.ReplyBody = "";

            await api.DeleteAsync(21);

            Assert.That(transport.Request.Method, Is.EqualTo("DELETE"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/vocab/21"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public long ReplyStatus { get; set; } = 200;

            public string ReplyBody { get; set; } = "{}";

            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(ApiTransportResponse.Received(ReplyStatus, ReplyBody));
            }
        }
    }
}
