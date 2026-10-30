using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class AiStatusApiTests
    {
        [Test]
        public async Task GetStatusAsync_ReadsWhetherConversationCanStartAndRemainingCount()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"status\":\"AI_AVAILABLE\",\"capabilities\":{\"canStartConversation\":true,"
                    + "\"canUseSpeech\":true,\"canGenerateFeedback\":true},\"remainingConversations\":2}",
            };
            var api = new AiStatusApi(new ApiClient("", transport));

            AiStatusData status = await api.GetStatusAsync();

            Assert.That(transport.Request.Method, Is.EqualTo("GET"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/ai/status"));
            Assert.That(status.Status, Is.EqualTo("AI_AVAILABLE"));
            Assert.That(status.Capabilities.CanStartConversation, Is.True);
            Assert.That(status.RemainingConversations, Is.EqualTo(2));
        }

        [Test]
        public async Task GetStatusAsync_Member_HasNoRemainingCount()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"status\":\"AI_AVAILABLE\",\"capabilities\":{\"canStartConversation\":true}}",
            };
            var api = new AiStatusApi(new ApiClient("", transport));

            AiStatusData status = await api.GetStatusAsync();

            Assert.That(status.RemainingConversations, Is.Null);
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
