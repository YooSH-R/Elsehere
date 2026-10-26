using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    // 요청 경로와 본문이 API 명세와 맞는지 본다. 응답 해석과 오류 처리는 ApiClientTests에서 검증한다.
    public class OnboardingApiTests
    {
        private RecordingTransport transport;
        private OnboardingApi api;

        [SetUp]
        public void CreateApi()
        {
            transport = new RecordingTransport();
            api = new OnboardingApi(new ApiClient("http://localhost:8080", transport));
        }

        [Test]
        public void GoogleLoginUrl_PointsToSocialLoginStart()
        {
            Assert.That(
                api.GoogleLoginUrl,
                Is.EqualTo("http://localhost:8080/api/v1/auth/login/google")
            );
        }

        [Test]
        public async Task CheckNickname_EncodesKoreanNicknameInQuery()
        {
            transport.Reply("{\"nickname\": \"선종\", \"available\": true}");

            await api.CheckNicknameAsync("선종");

            Assert.That(transport.LastRequest.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo(
                    "http://localhost:8080/api/v1/nicknames/availability?nickname=%EC%84%A0%EC%A2%85"
                )
            );
        }

        [Test]
        public async Task SkipLevelTest_PostsLanguageWithoutTestId()
        {
            transport.Reply("{\"testId\": 10, \"status\": \"SKIPPED\"}");

            await api.SkipLevelTestAsync("en-US");

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/level-tests/skip")
            );
            Assert.That(transport.LastRequest.JsonBody, Is.EqualTo("{\"languageCode\":\"en-US\"}"));
        }

        [Test]
        public async Task SelectLevel_PutsLevelToLanguageProfile()
        {
            transport.Reply(
                "{\"languageCode\": \"en-US\", \"selectedLevel\": 1, \"onboardingStep\": \"TUTORIAL\"}"
            );

            LevelSelectionResponse response = await api.SelectLevelAsync("en-US", 1);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("PUT"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/me/languages/en-US/level")
            );
            Assert.That(transport.LastRequest.JsonBody, Is.EqualTo("{\"selectedLevel\":1}"));
            Assert.That(response.OnboardingStep, Is.EqualTo(OnboardingStep.Tutorial));
        }

        private sealed class RecordingTransport : IApiTransport
        {
            private string nextBody;

            public ApiTransportRequest LastRequest { get; private set; }

            public void Reply(string body)
            {
                nextBody = body;
            }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                LastRequest = request;
                return Task.FromResult(ApiTransportResponse.Received(200, nextBody));
            }
        }
    }
}
