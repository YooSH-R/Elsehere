using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class ConversationApiTests
    {
        [TestCase(true, "\"npcOffDuty\":true")]
        [TestCase(false, "\"npcOffDuty\":false")]
        public async Task StartAsync_SendsWhetherTheNpcIsOffDuty(bool npcOffDuty, string expected)
        {
            var transport = new RecordingTransport
            {
                ReplyBody = "{\"conversationId\":58,\"npc\":{\"npcCode\":\"CAFE_STAFF\"}}",
            };
            var api = new ConversationApi(
                new ApiClient("", transport),
                new ApiClient("", transport)
            );

            await api.StartAsync("request-1", "CAFE_STAFF", "USER", npcOffDuty);

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/conversations"));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"npcCode\":\"CAFE_STAFF\""));
            Assert.That(transport.Request.JsonBody, Does.Contain(expected));
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
