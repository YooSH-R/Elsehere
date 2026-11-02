using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.Onboarding
{
    /// <summary>
    /// 입구 화면(01~05)의 진행. 서버 상태로 화면을 정하고, 화면에 필요한 데이터를 불러 둔 뒤 다음 화면으로 넘긴다.
    /// </summary>
    /// <remarks>
    /// 요청이 실패하면 화면을 그대로 두고 <see cref="ErrorMessage"/>에 서버 메시지를 담는다. 로그인이 풀렸으면(401) 로그인 화면으로 돌아가고,
    /// 접속 상태부터 읽지 못했으면 [다시 시도] 화면으로 간다.
    /// </remarks>
    public sealed class OnboardingFlow
    {
        private const string UnauthenticatedCode = "UNAUTHENTICATED";
        private const string NicknameTakenCode = "NICKNAME_TAKEN";
        private const string UnexpectedErrorMessage = "문제가 생겼어요. 다시 시도해 주세요.";

        private readonly IOnboardingApi api;

        public OnboardingFlow(IOnboardingApi api)
        {
            this.api = api;
        }

        public EntryScreen Screen { get; private set; } = EntryScreen.Login;

        public AvatarType? SelectedAvatar { get; private set; }

        /// <summary>계정에 저장한 화면 테마 코드. 마을에 들어가기 전에 화면 테마를 맞추는 데 쓴다.</summary>
        public string UiTheme { get; private set; }

        /// <summary>접속한 계정의 닉네임. 폰 메뉴 날짜 줄에 보인다.</summary>
        public string Nickname { get; private set; }

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

        /// <summary>
        /// 접속 상태를 읽어 첫 화면을 정한다. 로그인 외의 이유로 실패하면 보여 줄 화면을 정할 수 없으므로 [다시 시도] 화면으로 둔다.
        /// 그 화면의 [다시 시도]도 이 메서드를 다시 부른다.
        /// </summary>
        public async Task StartAsync()
        {
            await RunAsync(async () =>
            {
                BootstrapResponse boot = await api.GetBootstrapAsync();
                LanguageCode = boot.User?.CurrentLanguage;
                SelectedAvatar = boot.User?.AvatarType;
                UiTheme = boot.User?.UiTheme;
                Nickname = boot.User?.Nickname;
                IsVoiceAgreed = boot.User?.VoiceConsent ?? false;

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

            if (ErrorMessage != null)
            {
                Screen = EntryScreen.ConnectionFailed;
            }
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

        /// <summary>게스트 캐릭터 선택 화면에서 고른 캐릭터. 고르기 전에는 null이다.</summary>
        public AvatarType? GuestAvatar { get; private set; }

        /// <summary>화면 01의 [게스트로 시작하기]. 서버를 부르기 전에 캐릭터부터 고르게 한다.</summary>
        public void ChooseGuest()
        {
            GuestAvatar = null;
            GuestVoiceConsent = false;
            Screen = EntryScreen.GuestCharacter;
        }

        /// <summary>게스트가 NPC와 대화할 수 있는 횟수(AUTH-08). 읽기 전이나 읽지 못하면 null이고, 화면은 횟수 없이 안내한다.</summary>
        public int? GuestConversationLimit { get; private set; }

        /// <summary>
        /// 캐릭터 선택 화면에 보여 줄 대화 횟수를 읽는다. 읽지 못해도 캐릭터를 골라 시작할 수 있으므로 실패 안내를 남기지 않는다.
        /// </summary>
        public async Task LoadGuestGuideAsync()
        {
            try
            {
                GuestGuideResponse guide = await api.GetGuestGuideAsync();
                GuestConversationLimit = guide.ConversationLimit;
            }
            catch (ApiException)
            {
                // 서버가 답하지 못했으면 횟수 없이 안내한다.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void SelectGuestAvatar(AvatarType avatar)
        {
            GuestAvatar = avatar;
        }

        /// <summary>게스트 캐릭터 선택 화면의 음성 처리 동의(선택) 체크.</summary>
        public bool GuestVoiceConsent { get; private set; }

        public void SetGuestVoiceConsent(bool agreed)
        {
            GuestVoiceConsent = agreed;
        }

        /// <summary>지금 사용자가 음성 처리에 동의했는지. 동의하지 않았으면 대화에서 음성 입력을 막는다.</summary>
        public bool IsVoiceAgreed { get; private set; }

        /// <summary>캐릭터 선택 화면의 [뒤로]. 그 화면에서 실패한 안내는 로그인 화면에 남기지 않는다.</summary>
        public void BackToLogin()
        {
            ErrorMessage = null;
            Screen = EntryScreen.Login;
        }

        /// <summary>
        /// 고른 캐릭터로 게스트를 시작한 뒤 접속 상태를 다시 읽는다. 서버가 언어·난이도를 정해 두므로 튜토리얼로 이어진다.
        /// </summary>
        public async Task GuestLoginAsync()
        {
            if (GuestAvatar == null)
            {
                return;
            }

            AvatarType avatar = GuestAvatar.Value;
            bool voiceConsent = GuestVoiceConsent;
            await RunAsync(() => api.GuestLoginAsync(avatar, voiceConsent));
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
                    UiTheme = response.User?.UiTheme;
                    Nickname = response.User?.Nickname;
                    IsVoiceAgreed = response.User?.VoiceConsent ?? false;
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
                UiTheme = null;
                IsVoiceAgreed = false;
                Screen = EntryScreen.Login;
            }
            catch (ApiException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (Exception exception)
            {
                // 서버 오류가 아닌 문제도 화면이 멈춘 것처럼 보이지 않게 알리고, 원인은 로그로 남긴다.
                Debug.LogException(exception);
                ErrorMessage = UnexpectedErrorMessage;
            }
        }
    }
}
