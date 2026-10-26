using System.Runtime.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;

namespace A305.Api.Tests
{
    public class ApiClientTests
    {
        private const string BaseUrl = "http://localhost:8080";

        private FakeTransport transport;
        private ApiClient client;

        [SetUp]
        public void CreateClient()
        {
            transport = new FakeTransport();
            client = new ApiClient(BaseUrl, transport);
        }

        [Test]
        public async Task Get_SendsToBaseUrlWithoutCsrfHeaderAndReadsCamelCaseBody()
        {
            transport.Reply(200, "{\"userId\": 7, \"onboardingStep\": \"IN_PROGRESS\"}");

            Sample sample = await client.GetAsync<Sample>("/api/v1/bootstrap");

            Assert.That(transport.LastRequest.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/bootstrap")
            );
            Assert.That(
                transport.LastRequest.Headers.ContainsKey(ApiClient.CsrfHeaderName),
                Is.False
            );
            Assert.That(sample.UserId, Is.EqualTo(7));
            Assert.That(sample.OnboardingStep, Is.EqualTo(SampleStep.InProgress));
        }

        [Test]
        public async Task Post_AddsCsrfHeaderAndSendsCamelCaseJsonWithoutNulls()
        {
            transport.Reply(201, "{\"userId\": 1}");

            await client.PostAsync<Sample>(
                "/api/v1/auth/signup",
                new Sample { UserId = 1, OnboardingStep = SampleStep.InProgress }
            );

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Headers.ContainsKey(ApiClient.CsrfHeaderName),
                Is.True
            );
            Assert.That(
                transport.LastRequest.JsonBody,
                Is.EqualTo("{\"userId\":1,\"onboardingStep\":\"IN_PROGRESS\"}")
            );
        }

        [Test]
        public async Task Put_AddsCsrfHeader()
        {
            transport.Reply(200, "{\"userId\": 1}");

            await client.PutAsync<Sample>("/api/v1/me/languages/en-US", null);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("PUT"));
            Assert.That(
                transport.LastRequest.Headers.ContainsKey(ApiClient.CsrfHeaderName),
                Is.True
            );
        }

        [Test]
        public void ErrorResponse_ThrowsWithServerCodeStatusAndMessage()
        {
            transport.Reply(
                409,
                "{\"error\": {\"code\": \"NICKNAME_TAKEN\", \"message\": \"이미 사용 중인 닉네임입니다.\"}}"
            );

            ApiException exception = Assert.ThrowsAsync<ApiException>(() =>
                client.PostAsync<Sample>("/api/v1/auth/signup", new Sample())
            );

            Assert.That(exception.Code, Is.EqualTo("NICKNAME_TAKEN"));
            Assert.That(exception.StatusCode, Is.EqualTo(409));
            Assert.That(exception.Message, Is.EqualTo("이미 사용 중인 닉네임입니다."));
        }

        [Test]
        public void ErrorResponseNotInCommonFormat_ThrowsUnexpectedResponse()
        {
            transport.Reply(502, "<html>Bad Gateway</html>");

            ApiException exception = Assert.ThrowsAsync<ApiException>(() =>
                client.GetAsync<Sample>("/api/v1/bootstrap")
            );

            Assert.That(exception.Code, Is.EqualTo(ApiErrorCodes.UnexpectedResponse));
            Assert.That(exception.StatusCode, Is.EqualTo(502));
        }

        [Test]
        public void NetworkFailure_ThrowsNetworkError()
        {
            transport.FailNetwork();

            ApiException exception = Assert.ThrowsAsync<ApiException>(() =>
                client.GetAsync<Sample>("/api/v1/bootstrap")
            );

            Assert.That(exception.Code, Is.EqualTo(ApiErrorCodes.NetworkError));
            Assert.That(exception.StatusCode, Is.EqualTo(0));
        }

        [Test]
        public void PostWithoutResult_CompletesOnNoContent()
        {
            transport.Reply(204, "");

            Assert.DoesNotThrowAsync(() => client.PostAsync("/api/v1/auth/logout", null));
        }

        private enum SampleStep
        {
            [EnumMember(Value = "IN_PROGRESS")]
            InProgress,
        }

        private sealed class Sample
        {
            public long UserId { get; set; }
            public SampleStep? OnboardingStep { get; set; }
            public string Nickname { get; set; }
        }

        private sealed class FakeTransport : IApiTransport
        {
            private ApiTransportResponse nextResponse;

            public ApiTransportRequest LastRequest { get; private set; }

            public void Reply(long statusCode, string body)
            {
                nextResponse = ApiTransportResponse.Received(statusCode, body);
            }

            public void FailNetwork()
            {
                nextResponse = ApiTransportResponse.NetworkFailed();
            }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                LastRequest = request;
                return Task.FromResult(nextResponse);
            }
        }
    }
}
