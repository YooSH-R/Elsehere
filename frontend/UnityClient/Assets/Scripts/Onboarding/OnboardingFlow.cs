using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Onboarding
{
    /// <summary>
    /// 입구 화면(01~05)의 진행. 서버 상태로 화면을 정하고, 화면에 필요한 데이터를 불러 둔 뒤 다음 화면으로 넘긴다.
    /// </summary>
    /// <remarks>
    /// 요청이 실패하면 화면을 그대로 두고 <see cref="ErrorMessage"/>에 서버 메시지를 담는다. 로그인이 풀렸으면(401) 로그인 화면으로 돌아간다.
    /// </remarks>
    public sealed class OnboardingFlow
    {
        private const string UnauthenticatedCode = "UNAUTHENTICATED";
        private const string NicknameTakenCode = "NICKNAME_TAKEN";

        private readonly IOnboardingApi api;

        public OnboardingFlow(IOnboardingApi api)
        {
            this.api = api;
        }

        public EntryScreen Screen { get; private set; } = EntryScreen.Login;

        public AvatarType? SelectedAvatar { get; private set; }

        /// <summary>화면 02의 입력 상태. 가입 화면에 들어올 때 만든다.</summary>
        public SignupForm SignupForm { get; private set; }

        /// <summary>화면 03의 언어 카드.</summary>
        public IReadOnlyList<LanguageOption> Languages { get; private set; }

        /// <summary>화면 05의 난이도 카드와 추천 난이도.</summary>
        public LanguageProfileResponse LanguageProfile { get; private set; }

        /// <summary>지금 설정 중인 학습 언어.</summary>
        public string LanguageCode { get; private set; }

        /// <summary>재접속할 장소의 Unity 씬 이름. 없거나 열리지 않은 장소면 null이며, 이때는 기본 마을 씬을 연다.</summary>
        public string LastSceneName { get; private set; }

        /// <summary>마지막 요청이 실패한 이유. 다음 요청이 성공하면 지운다.</summary>
        public string ErrorMessage { get; private set; }

        public Task StartAsync()
        {
            return RunAsync(async () =>
            {
                BootstrapResponse boot = await api.GetBootstrapAsync();
                LanguageCode = boot.User?.CurrentLanguage;
                SelectedAvatar = boot.User?.AvatarType;

                LevelTestStatus? lastLevelTest = null;
                if (boot.User?.OnboardingStep == OnboardingStep.Level)
                {
                    LanguageProfile = await api.GetLanguageProfileAsync(LanguageCode);
                    lastLevelTest = LanguageProfile.LevelTest?.Status;
                }

                EntryScreen screen = EntryRouter.Route(boot, lastLevelTest);
                if (screen == EntryScreen.World && boot.LastSceneCode != null)
                {
                    LastSceneName = await FindOpenSceneAsync(boot.LastSceneCode);
                }

                await EnterAsync(screen);
            });
        }

        /// <summary>개발용 로그인 뒤 접속 상태를 다시 읽는다. Editor에서 Google 로그인 대신 쓴다.</summary>
        public async Task DevLoginAsync(long userId)
        {
            await RunAsync(() => api.DevLoginAsync(userId));
            if (ErrorMessage == null)
            {
                await StartAsync();
            }
        }

        /// <summary>게스트로 시작한 뒤 접속 상태를 다시 읽는다. 가입 화면 없이 언어 선택부터 이어진다.</summary>
        public async Task GuestLoginAsync()
        {
            await RunAsync(() => api.GuestLoginAsync());
            if (ErrorMessage == null)
            {
                await StartAsync();
            }
        }

        public Task CheckNicknameAsync()
        {
            string nickname = SignupForm.Nickname;
            return RunAsync(async () =>
            {
                NicknameAvailability result = await api.CheckNicknameAsync(nickname);
                SignupForm.RecordAvailability(nickname, result.Available);
            });
        }

        public Task SubmitSignupAsync()
        {
            return RunAsync(async () =>
            {
                try
                {
                    SignupResponse response = await api.SignUpAsync(SignupForm.ToRequest());
                    SelectedAvatar = response.User?.AvatarType;
                }
                catch (ApiException exception) when (exception.Code == NicknameTakenCode)
                {
                    // 중복 확인 뒤 제출 전에 다른 사용자가 먼저 가입했다. 다시 확인하도록 상태를 돌려놓는다.
                    SignupForm.RecordAvailability(SignupForm.Nickname, false);
                    throw;
                }

                await EnterAsync(EntryScreen.Language);
            });
        }

        public Task ChooseLanguageAsync(string languageCode)
        {
            return RunAsync(async () =>
            {
                await api.ChooseLanguageAsync(languageCode);
                LanguageCode = languageCode;
                await EnterAsync(EntryScreen.LevelTest);
            });
        }

        public Task SkipLevelTestAsync()
        {
            return RunAsync(async () =>
            {
                await api.SkipLevelTestAsync(LanguageCode);
                await EnterAsync(EntryScreen.LevelSetting);
            });
        }

        public Task SelectLevelAsync(int level)
        {
            return RunAsync(async () =>
            {
                await api.SelectLevelAsync(LanguageCode, level);
                await EnterAsync(EntryScreen.Tutorial);
            });
        }

        // BOOT-01은 장소 코드만 주므로 장소 목록(WORLD-01)에서 Unity 씬 이름을 찾는다. 닫혔거나 목록에 없으면 null이다.
        private async Task<string> FindOpenSceneAsync(string sceneCode)
        {
            List<SceneSummary> scenes = await api.GetScenesAsync();
            SceneSummary scene = scenes.Find(candidate => candidate.SceneCode == sceneCode);
            return scene != null && scene.IsOpen ? scene.UnityKey : null;
        }

        // 화면에 보여 줄 데이터를 먼저 불러 두고 화면을 바꾼다.
        private async Task EnterAsync(EntryScreen screen)
        {
            switch (screen)
            {
                case EntryScreen.Signup:
                    SignupFormResponse form = await api.GetSignupFormAsync();
                    SignupForm = new SignupForm(form.Consents);
                    break;
                case EntryScreen.Language:
                    Languages = await api.GetLanguagesAsync();
                    break;
                case EntryScreen.LevelSetting:
                    LanguageProfile = await api.GetLanguageProfileAsync(LanguageCode);
                    break;
            }

            Screen = screen;
        }

        private async Task RunAsync(Func<Task> action)
        {
            ErrorMessage = null;
            try
            {
                await action();
            }
            catch (ApiException exception) when (exception.Code == UnauthenticatedCode)
            {
                SelectedAvatar = null;
                Screen = EntryScreen.Login;
            }
            catch (ApiException exception)
            {
                ErrorMessage = exception.Message;
            }
        }
    }
}
