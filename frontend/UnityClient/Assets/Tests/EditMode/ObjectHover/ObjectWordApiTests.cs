using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordApiTests
    {
        private const string SceneJson =
            "{\"sceneCode\":\"HOME\",\"name\":\"집\",\"unityKey\":\"HomeScene\",\"isOpen\":true,\"npcs\":[],"
            + "\"words\":[{\"wordKey\":\"sofa\",\"vocab\":{\"vocabId\":31,\"type\":\"WORD\",\"text\":\"sofa\","
            + "\"meaningKo\":\"소파\",\"example\":\"He is sitting on the sofa.\",\"exampleKo\":\"그는 소파에 앉아 있어요.\"},"
            + "\"hasPronunciation\":true,\"saved\":false,\"hoverHidden\":true}]}";

        private RecordingTransport transport;
        private ObjectWordApi api;

        [SetUp]
        public void CreateApi()
        {
            transport = new RecordingTransport();
            api = new ObjectWordApi(new ApiClient("", transport));
        }

        [Test]
        public async Task GetSceneWordsAsync_ReadsWordCardsOfScene()
        {
            transport.ReplyBody = SceneJson;

            SceneWords scene = await api.GetSceneWordsAsync("HOME");

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/scenes/HOME"));
            WordCardData card = scene.Words[0];
            Assert.That(card.WordKey, Is.EqualTo("sofa"));
            Assert.That(card.Vocab.VocabId, Is.EqualTo(31));
            Assert.That(card.Vocab.Text, Is.EqualTo("sofa"));
            Assert.That(card.Vocab.MeaningKo, Is.EqualTo("소파"));
            Assert.That(card.Vocab.Example, Is.EqualTo("He is sitting on the sofa."));
            Assert.That(card.Vocab.ExampleKo, Is.EqualTo("그는 소파에 앉아 있어요."));
            Assert.That(card.HasPronunciation, Is.True);
            Assert.That(card.Saved, Is.False);
            Assert.That(card.HoverHidden, Is.True);
        }

        [Test]
        public async Task SaveAsync_SavesObjectWordByWordKey()
        {
            transport.ReplyStatus = 201;

            await api.SaveAsync("sofa", "request-1");

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/vocab"));
            Assert.That(
                transport.Request.JsonBody,
                Is.EqualTo(
                    "{\"requestId\":\"request-1\",\"sourceType\":\"OBJECT\",\"wordKey\":\"sofa\"}"
                )
            );
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        [Test]
        public async Task SetHoverHiddenAsync_PutsWordPreference()
        {
            transport.ReplyBody = "{\"wordKey\":\"sofa\",\"hoverHidden\":true}";

            await api.SetHoverHiddenAsync("sofa", true);

            Assert.That(transport.Request.Method, Is.EqualTo("PUT"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/me/word-prefs/sofa"));
            Assert.That(transport.Request.JsonBody, Is.EqualTo("{\"hoverHidden\":true}"));
        }

        [Test]
        public void PronunciationPath_UsesVocabId()
        {
            Assert.That(
                ObjectWordApi.PronunciationPath(31),
                Is.EqualTo("/api/v1/vocab/31/pronunciation")
            );
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
