using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Onboarding
{
    /// <summary>입구 화면과 로그아웃이 쓰는 API의 경로와 본문. 경로는 API 명세(docs/api/mvp-api.md)를 따른다.</summary>
    public sealed class OnboardingApi : IOnboardingApi, IMyPageApi, ILogoutApi
    {
        private const string ConsentsPath = "/api/v1/me/consents";

        private readonly ApiClient client;

        public OnboardingApi(ApiClient client)
        {
            this.client = client;
        }

        public string GoogleLoginUrl => client.UrlFor("/api/v1/auth/login/google");

        public Task<BootstrapResponse> GetBootstrapAsync()
        {
            return client.GetAsync<BootstrapResponse>("/api/v1/bootstrap");
        }

        public Task DevLoginAsync(long userId)
        {
            return client.PostAsync("/api/v1/dev/login", new DevLoginRequest { UserId = userId });
        }

        public Task LogoutAsync()
        {
            return client.PostAsync("/api/v1/auth/logout", null);
        }

        public Task WithdrawAsync()
        {
            return client.DeleteAsync("/api/v1/me");
        }

        public Task<NicknameChange> ChangeNicknameAsync(string nickname)
        {
            return client.PutAsync<NicknameChange>(
                "/api/v1/me/nickname",
                new NicknameChange { Nickname = nickname }
            );
        }

        public Task GuestLoginAsync(AvatarType avatarType, bool voiceConsent)
        {
            return client.PostAsync(
                "/api/v1/auth/guest",
                new GuestLoginRequest { AvatarType = avatarType, VoiceConsent = voiceConsent }
            );
        }

        public async Task<List<ConsentStatus>> GetConsentsAsync()
        {
            ConsentsResponse response = await client.GetAsync<ConsentsResponse>(ConsentsPath);
            return response.Consents;
        }

        public Task<ConsentStatus> ChangeConsentAsync(string consentType, bool agreed)
        {
            return client.PutAsync<ConsentStatus>(
                ConsentsPath,
                new ConsentChangeRequest { ConsentType = consentType, Agreed = agreed }
            );
        }

        public Task<SignupFormResponse> GetSignupFormAsync()
        {
            return client.GetAsync<SignupFormResponse>("/api/v1/auth/signup");
        }

        public Task<NicknameAvailability> CheckNicknameAsync(string nickname)
        {
            return client.GetAsync<NicknameAvailability>(
                "/api/v1/nicknames/availability?nickname=" + Uri.EscapeDataString(nickname)
            );
        }

        public Task<SignupResponse> SignUpAsync(SignupRequest request)
        {
            return client.PostAsync<SignupResponse>("/api/v1/auth/signup", request);
        }

        public Task<List<LanguageOption>> GetLanguagesAsync()
        {
            return client.GetAsync<List<LanguageOption>>("/api/v1/languages");
        }

        public Task<LanguageSelectionResponse> ChooseLanguageAsync(string languageCode)
        {
            return client.PutAsync<LanguageSelectionResponse>(LanguagePath(languageCode), null);
        }

        public Task<LanguageProfileResponse> GetLanguageProfileAsync(string languageCode)
        {
            return client.GetAsync<LanguageProfileResponse>(LanguagePath(languageCode));
        }

        public Task<LevelTestResultResponse> SkipLevelTestAsync(string languageCode)
        {
            return client.PostAsync<LevelTestResultResponse>(
                "/api/v1/level-tests/skip",
                new LevelTestLanguageRequest { LanguageCode = languageCode }
            );
        }

        public Task<LevelSelectionResponse> SelectLevelAsync(string languageCode, int level)
        {
            return client.PutAsync<LevelSelectionResponse>(
                LanguagePath(languageCode) + "/level",
                new LevelSelectionRequest { SelectedLevel = level }
            );
        }

        public Task<List<SceneSummary>> GetScenesAsync()
        {
            return client.GetAsync<List<SceneSummary>>("/api/v1/scenes");
        }

        private static string LanguagePath(string languageCode)
        {
            return "/api/v1/me/languages/" + Uri.EscapeDataString(languageCode);
        }
    }
}
