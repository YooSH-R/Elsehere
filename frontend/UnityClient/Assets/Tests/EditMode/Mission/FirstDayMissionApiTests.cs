using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class FirstDayMissionApiTests
    {
        [Test]
        public async Task SceneVisitRestoresServerProgressWithoutSendingClientCompletion()
        {
            var transport = new RecordingTransport();
            var api = new FirstDayMissionApi(new ApiClient("", transport));
            FirstDayMissionState state = await api.VisitAsync("CAFE");
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Stage, Is.EqualTo(3));
            Assert.That(state.Steps[0].Goals[0].Completed, Is.True);
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/missions/first-day/visit"));
            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            string body = transport.Request.JsonBody;
            Assert.That(body, Does.Contain("\"scene\":\"CAFE\""));
            Assert.That(body, Does.Not.Contain("stage"));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(
                    ApiTransportResponse.Received(
                        200,
                        "{\"started\":true,\"stage\":3,\"steps\":[{\"goals\":[{\"completed\":true}]}]}"
                    )
                );
            }
        }
    }
}
