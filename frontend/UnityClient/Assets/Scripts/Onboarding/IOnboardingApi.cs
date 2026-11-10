using System.Collections.Generic;
using System.Threading.Tasks;

namespace A305.Onboarding
{
    /// <summary>입구 화면이 쓰는 서버 API(BOOT-01, AUTH, ONB, WORLD-01). 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface IOnboardingApi
    {
        /// <summary>AUTH-01. 페이지 이동으로 여는 Google 로그인 주소.</summary>
        string GoogleLoginUrl { get; }

        Task<BootstrapResponse> GetBootstrapAsync();

        /// <summary>개발용 로그인. 백엔드 local 프로필에서만 동작한다.</summary>
        Task DevLoginAsync(long userId);

        /// <summary>AUTH-08. 게스트가 대화할 수 있는 횟수. 로그인하지 않아도 부를 수 있다.</summary>
        Task<GuestGuideResponse> GetGuestGuideAsync();

        /// <summary>
        /// AUTH-07. 고른 캐릭터와 음성 처리 동의(선택)로 게스트를 시작한다. 서버가 새 사용자를 만들고 그 사용자로 로그인한다.
        /// </summary>
        Task GuestLoginAsync(AvatarType avatarType, bool voiceConsent);

        Task<SignupFormResponse> GetSignupFormAsync();

        Task<NicknameAvailability> CheckNicknameAsync(string nickname);

        Task<SignupResponse> SignUpAsync(SignupRequest request);

        Task<List<LanguageOption>> GetLanguagesAsync();

        Task<LanguageSelectionResponse> ChooseLanguageAsync(string languageCode);

        Task<LanguageProfileResponse> GetLanguageProfileAsync(string languageCode);

        Task<LevelTestResultResponse> SkipLevelTestAsync(string languageCode);

        /// <summary>ONB-03. [시작하기]마다 새 테스트를 만들고 문항을 받는다.</summary>
        Task<LevelTestStartResponse> StartLevelTestAsync(string languageCode);

        /// <summary>ONB-04. 같은 문항에 다시 답하면 서버가 덮어쓴다.</summary>
        Task AnswerLevelTestAsync(long testId, string itemCode, string answer);

        /// <summary>ONB-05. 채점해 추천 난이도를 정한다. 말하기 판정에 실패하면 테스트는 진행 중으로 남아 다시 부를 수 있다.</summary>
        Task<LevelTestResultResponse> CompleteLevelTestAsync(long testId);

        /// <summary>ONB-06. 푸는 도중 [그만하기]. 추천 난이도는 바꾸지 않는다.</summary>
        Task<LevelTestResultResponse> QuitLevelTestAsync(long testId);

        /// <summary>CONV-02(<c>purpose: LEVEL_TEST</c>). 말하기 답 녹음(16kHz 모노 WAV)을 받아 적는다.</summary>
        Task<SpeechTranscription> TranscribeLevelTestAsync(string requestId, byte[] wav);

        Task<LevelSelectionResponse> SelectLevelAsync(string languageCode, int level);

        /// <summary>WORLD-01. 재접속할 장소의 Unity 씬 이름을 찾는다.</summary>
        Task<List<SceneSummary>> GetScenesAsync();
    }
}
