using System.Collections.Generic;
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
        public async Task ChangeNickname_PutsNicknameToMe()
        {
            transport.Reply("{\"nickname\": \"선종\"}");

            NicknameChange changed = await api.ChangeNicknameAsync("선종");

            Assert.That(transport.LastRequest.Method, Is.EqualTo("PUT"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/me/nickname")
            );
            Assert.That(transport.LastRequest.JsonBody, Is.EqualTo("{\"nickname\":\"선종\"}"));
            Assert.That(changed.Nickname, Is.EqualTo("선종"));
        }

        [Test]
        public async Task Logout_PostsLogoutWithoutBody()
        {
            transport.Reply("");

            await api.LogoutAsync();

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/auth/logout")
            );
            Assert.That(transport.LastRequest.JsonBody, Is.Null);
            Assert.That(
                transport.LastRequest.Headers.ContainsKey(ApiClient.CsrfHeaderName),
                Is.True
            );
        }

        [Test]
        public async Task GuestLogin_PostsChosenCharacterToGuestStart()
        {
            transport.Reply("{\"user\": {\"userId\": 3, \"nickname\": \"guest-123456\"}}");

            await api.GuestLoginAsync(AvatarType.M);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/auth/guest")
            );
            Assert.That(transport.LastRequest.JsonBody, Is.EqualTo("{\"avatarType\":\"M\"}"));
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

        [Test]
        public async Task GetScenes_ReadsUnitySceneOfEachPlace()
        {
            transport.Reply(
                "[{\"sceneCode\": \"CAFE\", \"name\": \"모퉁이 카페\", \"unityKey\": \"CafeScene\", \"isOpen\": true}]"
            );

            List<SceneSummary> scenes = await api.GetScenesAsync();

            Assert.That(transport.LastRequest.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/scenes")
            );
            Assert.That(scenes[0].SceneCode, Is.EqualTo("CAFE"));
            Assert.That(scenes[0].UnityKey, Is.EqualTo("CafeScene"));
            Assert.That(scenes[0].IsOpen, Is.True);
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
