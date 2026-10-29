using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Theme.Tests
{
    public class UiThemeApiTests
    {
        [Test]
        public async Task SaveAsync_Theme_PutsServerCodeAndReadsSavedTheme()
        {
            var transport = new RecordingTransport("{\"uiTheme\":\"STICKER\"}");
            var api = new UiThemeApi(new ApiClient("", transport));

            UiThemeId saved = await api.SaveAsync(UiThemeId.Sticker);

            Assert.That(saved, Is.EqualTo(UiThemeId.Sticker));
            Assert.That(transport.Request.Method, Is.EqualTo("PUT"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/me/ui-theme"));
            Assert.That(transport.Request.JsonBody, Does.Contain("\"uiTheme\":\"STICKER\""));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        private sealed class RecordingTransport : IApiTransport
        {
            private readonly string responseBody;

            public RecordingTransport(string responseBody)
            {
                this.responseBody = responseBody;
            }

            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(ApiTransportResponse.Received(200, responseBody));
            }
        }
    }
}
