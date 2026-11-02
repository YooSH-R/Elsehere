using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class VoiceConsentApiTests
    {
        [Test]
        public async Task AgreeAsync_TurnsOnVoiceConsent()
        {
            var transport = new RecordingTransport
            {
                ReplyBody =
                    "{\"consentType\":\"VOICE\",\"title\":\"음성 처리 동의\",\"version\":\"2026-10\","
                    + "\"agreed\":true}",
            };
            var api = new VoiceConsentApi(new ApiClient("", transport));

            await api.AgreeAsync();

            Assert.That(transport.Request.Method, Is.EqualTo("PUT"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/me/consents"));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"consentType\":\"VOICE\""));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"agreed\":true"));
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
