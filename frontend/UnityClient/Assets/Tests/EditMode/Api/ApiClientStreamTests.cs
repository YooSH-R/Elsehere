using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace A305.Api.Tests
{
    public class ApiClientStreamTests
    {
        private const string BaseUrl = "http://localhost:8080";

        private FakeStreamTransport stream;
        private ApiClient client;
        private List<string> events;

        [SetUp]
        public void CreateClient()
        {
            stream = new FakeStreamTransport();
            client = new ApiClient(BaseUrl, new UnusedTransport(), stream);
            events = new List<string>();
        }

        [Test]
        public async Task PostStream_AsksForEventStreamWithCsrfHeaderAndJsonBody()
        {
            stream.Reply(200);

            await client.PostStreamAsync("/api/v1/sample", new { RequestId = "r-1" }, Record);

            Assert.That(stream.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(stream.LastRequest.Url, Is.EqualTo(BaseUrl + "/api/v1/sample"));
            Assert.That(stream.LastRequest.JsonBody, Is.EqualTo("{\"requestId\":\"r-1\"}"));
            Assert.That(stream.LastRequest.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
            // 스트림을 시작하기 전의 오류는 JSON으로 오므로 둘 다 받는다고 알린다.
            Assert.That(
                stream.LastRequest.Headers["Accept"],
                Is.EqualTo("text/event-stream, application/json")
            );
        }

        [Test]
        public async Task PostStream_PassesEventsInOrderEvenWhenTextArrivesInPieces()
        {
            stream.Reply(
                200,
                "event: delta\nda",
                "ta: {\"text\":\"Hi\"}\n\nevent: audio\ndata: {\"seq\":0}\n",
                "\nevent: turn\r\ndata: {\"ok\":true}\r\n\r\n"
            );

            await client.PostStreamAsync("/api/v1/sample", null, Record);

            Assert.That(
                events,
                Is.EqualTo(
                    new[] { "delta={\"text\":\"Hi\"}", "audio={\"seq\":0}", "turn={\"ok\":true}" }
                )
            );
        }

        [Test]
        public void PostStream_ErrorStatus_ThrowsServerErrorWithoutEvents()
        {
            stream.Reply(
                409,
                "{\"error\": {\"code\": \"CONVERSATION_NOT_ACTIVE\", \"message\": \"끝난 대화\"}}"
            );

            var exception = Assert.ThrowsAsync<ApiException>(() =>
                client.PostStreamAsync("/api/v1/sample", null, Record)
            );

            Assert.That(exception.Code, Is.EqualTo("CONVERSATION_NOT_ACTIVE"));
            Assert.That(exception.StatusCode, Is.EqualTo(409));
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void PostStream_NetworkFailure_ThrowsNetworkError()
        {
            stream.FailNetwork();

            var exception = Assert.ThrowsAsync<ApiException>(() =>
                client.PostStreamAsync("/api/v1/sample", null, Record)
            );

            Assert.That(exception.Code, Is.EqualTo(ApiErrorCodes.NetworkError));
            Assert.That(exception.StatusCode, Is.EqualTo(0));
        }

        private void Record(string name, string data)
        {
            events.Add(name + "=" + data);
        }

        private sealed class FakeStreamTransport : IApiStreamTransport
        {
            private ApiTransportResponse response;
            private string[] pieces = Array.Empty<string>();

            public ApiTransportRequest LastRequest { get; private set; }

            public void Reply(long statusCode, params string[] bodyPieces)
            {
                bool ok = statusCode >= 200 && statusCode < 300;
                response = ApiTransportResponse.Received(
                    statusCode,
                    ok ? null : string.Concat(bodyPieces)
                );
                pieces = ok ? bodyPieces : Array.Empty<string>();
            }

            public void FailNetwork()
            {
                response = ApiTransportResponse.NetworkFailed();
            }

            public Task<ApiTransportResponse> SendStreamAsync(
                ApiTransportRequest request,
                Action<string> onText
            )
            {
                LastRequest = request;
                foreach (string piece in pieces)
                {
                    onText(piece);
                }

                return Task.FromResult(response);
            }
        }

        private sealed class UnusedTransport : IApiTransport
        {
            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                throw new AssertionException("스트림 요청은 일반 전송을 쓰지 않는다.");
            }
        }
    }
}
