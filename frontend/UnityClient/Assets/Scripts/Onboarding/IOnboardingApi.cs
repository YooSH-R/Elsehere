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

        Task<SignupFormResponse> GetSignupFormAsync();

        Task<NicknameAvailability> CheckNicknameAsync(string nickname);

        Task<SignupResponse> SignUpAsync(SignupRequest request);

        Task<List<LanguageOption>> GetLanguagesAsync();

        Task<LanguageSelectionResponse> ChooseLanguageAsync(string languageCode);

        Task<LanguageProfileResponse> GetLanguageProfileAsync(string languageCode);

        Task<LevelTestResultResponse> SkipLevelTestAsync(string languageCode);

        Task<LevelSelectionResponse> SelectLevelAsync(string languageCode, int level);

        /// <summary>WORLD-01. 재접속할 장소의 Unity 씬 이름을 찾는다.</summary>
        Task<List<SceneSummary>> GetScenesAsync();
    }
}
