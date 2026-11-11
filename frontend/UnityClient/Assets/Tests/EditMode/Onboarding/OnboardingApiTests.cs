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
        public async Task Withdraw_DeletesMe()
        {
            transport.Reply("");

            await api.WithdrawAsync();

            Assert.That(transport.LastRequest.Method, Is.EqualTo("DELETE"));
            Assert.That(transport.LastRequest.Url, Is.EqualTo("http://localhost:8080/api/v1/me"));
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public async Task Bootstrap_ReadsWhetherUserStartedAsGuest(string guest, bool expected)
        {
            transport.Reply(
                "{\"authState\": \"SIGNED_IN\", \"user\": {\"userId\": 3, \"guest\": "
                    + guest
                    + "}}"
            );

            BootstrapResponse boot = await api.GetBootstrapAsync();

            Assert.That(boot.User.Guest, Is.EqualTo(expected));
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
        public async Task GuestLogin_PostsChosenCharacterAndVoiceConsentToGuestStart()
        {
            transport.Reply("{\"user\": {\"userId\": 3, \"nickname\": \"guest-123456\"}}");

            await api.GuestLoginAsync(AvatarType.M, true);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/auth/guest")
            );
            Assert.That(
                transport.LastRequest.JsonBody,
                Is.EqualTo("{\"avatarType\":\"M\",\"voiceConsent\":true}")
            );
        }

        [Test]
        public async Task Consents_GetsConsentsUserCanChange()
        {
            transport.Reply(
                "{\"consents\": [{\"consentType\": \"VOICE\", \"version\": \"2026-10\","
                    + " \"title\": \"음성 처리 동의\", \"url\": \"/voice/2026-10\", \"agreed\": true}]}"
            );

            List<ConsentStatus> consents = await api.GetConsentsAsync();

            Assert.That(transport.LastRequest.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/me/consents")
            );
            Assert.That(consents[0].ConsentType, Is.EqualTo("VOICE"));
            Assert.That(consents[0].Agreed, Is.True);
        }

        [Test]
        public async Task ChangeConsent_PutsConsentTypeAndChoice()
        {
            transport.Reply(
                "{\"consentType\": \"VOICE\", \"version\": \"2026-10\", \"title\": \"음성 처리 동의\","
                    + " \"url\": \"/voice/2026-10\", \"agreed\": false}"
            );

            ConsentStatus changed = await api.ChangeConsentAsync("VOICE", false);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("PUT"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/me/consents")
            );
            Assert.That(
                transport.LastRequest.JsonBody,
                Is.EqualTo("{\"consentType\":\"VOICE\",\"agreed\":false}")
            );
            Assert.That(changed.Agreed, Is.False);
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
        public async Task StartLevelTest_PostsLanguageAndReadsListeningAndSpeakingItems()
        {
            transport.Reply(
                "{\"testId\": 9, \"status\": \"IN_PROGRESS\", \"totalItems\": 6, \"items\": ["
                    + "{\"itemCode\": \"EN_LT_L2\", \"type\": \"LISTEN\", \"instruction\": \"잘 듣고 무슨 뜻인지 고르세요.\","
                    + " \"question\": \"무슨 뜻일까요?\", \"choices\": [\"가\", \"나\", \"다\", \"라\"],"
                    + " \"speaker\": \"Gus · 빨래방 주인\", \"cues\": []},"
                    + "{\"itemCode\": \"EN_LT_S1\", \"type\": \"SPEAK\", \"instruction\": \"상황에 맞게 말해 보세요.\","
                    + " \"question\": \"카페에 가방을 두고 왔어요.\", \"choices\": [], \"cues\": [\"파란 숄더백\"]}]}"
            );

            LevelTestStartResponse started = await api.StartLevelTestAsync("en-US");

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/level-tests")
            );
            Assert.That(transport.LastRequest.JsonBody, Is.EqualTo("{\"languageCode\":\"en-US\"}"));
            Assert.That(started.TestId, Is.EqualTo(9));
            Assert.That(started.TotalItems, Is.EqualTo(6));
            Assert.That(started.Items[0].Type, Is.EqualTo(LevelTestItemType.Listen));
            Assert.That(started.Items[0].Choices, Has.Count.EqualTo(4));
            Assert.That(started.Items[0].Speaker, Is.EqualTo("Gus · 빨래방 주인"));
            Assert.That(started.Items[1].Type, Is.EqualTo(LevelTestItemType.Speak));
            Assert.That(started.Items[1].Speaker, Is.Null);
            Assert.That(started.Items[1].Cues, Is.EqualTo(new[] { "파란 숄더백" }));
        }

        [Test]
        public async Task AnswerLevelTest_PutsAnswerToTheItem()
        {
            transport.Reply("");

            await api.AnswerLevelTestAsync(9, "EN_LT_S1", "I left my bag here.");

            Assert.That(transport.LastRequest.Method, Is.EqualTo("PUT"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/level-tests/9/answers/EN_LT_S1")
            );
            Assert.That(
                transport.LastRequest.JsonBody,
                Is.EqualTo("{\"answer\":\"I left my bag here.\"}")
            );
        }

        [Test]
        public async Task CompleteLevelTest_PostsCompleteAndReadsScoresAndFeedback()
        {
            transport.Reply(
                "{\"testId\": 9, \"status\": \"DONE\", \"score\": 5, \"resultLevel\": 2,"
                    + " \"listeningScore\": 3, \"speakingPassed\": 2, \"feedback\": \"설명은 잘했어요.\"}"
            );

            LevelTestResultResponse result = await api.CompleteLevelTestAsync(9);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/level-tests/9/complete")
            );
            Assert.That(result.Status, Is.EqualTo(LevelTestStatus.Done));
            Assert.That(result.ResultLevel, Is.EqualTo(2));
            Assert.That(result.ListeningScore, Is.EqualTo(3));
            Assert.That(result.SpeakingPassed, Is.EqualTo(2));
            Assert.That(result.Feedback, Is.EqualTo("설명은 잘했어요."));
        }

        [Test]
        public async Task QuitLevelTest_PostsQuit()
        {
            transport.Reply("{\"testId\": 9, \"status\": \"QUIT\"}");

            LevelTestResultResponse result = await api.QuitLevelTestAsync(9);

            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/level-tests/9/quit")
            );
            Assert.That(result.Status, Is.EqualTo(LevelTestStatus.Quit));
        }

        [Test]
        public void LevelTestSpeechPath_PointsToTheItemSpeech()
        {
            Assert.That(
                OnboardingApi.LevelTestSpeechPath("EN_LT_L2"),
                Is.EqualTo("/api/v1/level-tests/items/EN_LT_L2/speech")
            );
        }

        // 레벨 테스트 말하기 답은 대화가 없으므로 conversationId 없이 LEVEL_TEST로 받아 적는다(CONV-02).
        [Test]
        public async Task TranscribeLevelTest_PostsWavAsLevelTestWithoutConversation()
        {
            transport.Reply("{\"sttRawText\": \"I left my bag here.\", \"durationMs\": 2300}");

            SpeechTranscription transcription = await api.TranscribeLevelTestAsync(
                "request-1",
                new byte[] { 1, 2, 3 }
            );

            string body = System.Text.Encoding.UTF8.GetString(transport.LastRequest.RawBody);
            Assert.That(transport.LastRequest.Method, Is.EqualTo("POST"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/speech/transcriptions")
            );
            Assert.That(body, Does.Contain("name=\"purpose\"\r\n\r\nLEVEL_TEST\r\n"));
            Assert.That(body, Does.Contain("name=\"requestId\"\r\n\r\nrequest-1\r\n"));
            Assert.That(body, Does.Contain("Content-Type: audio/wav"));
            Assert.That(body, Does.Not.Contain("conversationId"));
            Assert.That(transcription.SttRawText, Is.EqualTo("I left my bag here."));
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

        [Test]
        public async Task GuestGuide_GetsConversationLimitBeforeLogin()
        {
            transport.Reply("{\"conversationLimit\": 3}");

            GuestGuideResponse guide = await api.GetGuestGuideAsync();

            Assert.That(transport.LastRequest.Method, Is.EqualTo("GET"));
            Assert.That(
                transport.LastRequest.Url,
                Is.EqualTo("http://localhost:8080/api/v1/auth/guest")
            );
            Assert.That(guide.ConversationLimit, Is.EqualTo(3));
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
