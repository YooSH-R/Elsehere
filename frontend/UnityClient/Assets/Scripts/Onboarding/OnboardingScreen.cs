using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Player;
using A305.Theme;
using A305.Tutorial;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.Onboarding
{
    /// <summary>
    /// 입구 화면(01~05)을 잇는다. 진행 판단은 <see cref="OnboardingFlow"/>에 맡기고, 패널 입력을 전달해 결과 화면을 보여 준다.
    /// 튜토리얼 단계가 되면 집 씬, 초기 설정을 마친 사용자는 마을 씬으로 보낸다.
    /// </summary>
    public class OnboardingScreen : MonoBehaviour
    {
        private const string LoginErrorQuery = "loginError=true";
        private const string ExistingAccountQuery = "existingAccount=true";
        private const string CheckingMessage = "접속 상태를 확인하고 있어요.";
        private const string WaitingMessage = "잠시만 기다려 주세요...";
        private const string GradingMessage = "채점하고 있어요. 잠시만 기다려 주세요...";
        private const string TranscribingMessage = "말한 내용을 받아 적고 있어요...";

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private OnboardingSettings settings;

        [SerializeField]
        [Tooltip("요청을 기다리는 동안 두 번 누르지 않게 입력을 막는다")]
        private CanvasGroup inputGroup;

        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private LoginPanelView loginPanel;

        [SerializeField]
        private GuestPanelView guestPanel;

        [SerializeField]
        [Tooltip("가입의 첫 단계: 약관 동의")]
        private TermsPanelView termsPanel;

        [SerializeField]
        private SignupPanelView signupPanel;

        [SerializeField]
        private LanguagePanelView languagePanel;

        [SerializeField]
        private LevelTestPanelView levelTestPanel;

        [SerializeField]
        private LevelSettingPanelView levelSettingPanel;

        [SerializeField]
        private ConnectionPanelView connectionPanel;

        [SerializeField]
        [Tooltip(
            "게스트로 다시 접속했거나 이미 가입한 계정으로 들어왔을 때 게임에 들어가기 전에 보이는 화면"
        )]
        private GuestReturnPanelView guestReturnPanel;

        [SerializeField]
        [Tooltip("입구 화면 뒤 배경. 가운데 창이 뜨면 왼쪽 크림색 그라데이션 없이 흐린 마을만 보여 준다.")]
        private EntryBackdropView backdrop;

        private IOnboardingApi api;
        private OnboardingFlow flow;

        private void Awake()
        {
            PlayerCharacterSelection.Reset();
            PlayerNickname.Reset();
            api = new OnboardingApi(apiSettings.CreateClient());
            flow = new OnboardingFlow(api);
            ConnectPanels();
            ShowOnly(null);
        }

        private void Start()
        {
            Run(() => flow.StartAsync(CameFromExistingAccount()), CheckingMessage);
        }

        private void ConnectPanels()
        {
            connectionPanel.RetryRequested += () => Run(() => flow.StartAsync(), CheckingMessage);
            loginPanel.GoogleLoginRequested += () => BrowserRedirect.GoTo(api.GoogleLoginUrl);
            guestReturnPanel.SignupRequested += () => BrowserRedirect.GoTo(api.GoogleLoginUrl);
            guestReturnPanel.ContinueRequested += () =>
            {
                flow.ContinueToGame();
                Render();
            };
            loginPanel.GuestLoginRequested += () =>
            {
                flow.ChooseGuest();
                Render();
                ShowGuestConversationLimit();
            };
            loginPanel.DevLoginRequested += () =>
                Run(() => flow.DevLoginAsync(settings.DevLoginUserId));
            loginPanel.DocumentRequested += Application.OpenURL;

            guestPanel.AvatarSelected += avatar =>
            {
                flow.SelectGuestAvatar(avatar);
                ShowGuestPanel();
            };
            guestPanel.VoiceConsentChanged += flow.SetGuestVoiceConsent;
            guestPanel.DocumentRequested += Application.OpenURL;
            guestPanel.StartRequested += () => Run(flow.GuestLoginAsync);
            guestPanel.BackRequested += () =>
            {
                flow.BackToLogin();
                Render();
            };

            termsPanel.AllAgreeChanged += agreed => EditTerms(form => form.AgreeAll(agreed));
            termsPanel.ConsentChanged += (consentType, agreed) =>
                EditTerms(form => form.SetAgreed(consentType, agreed));
            termsPanel.DocumentRequested += Application.OpenURL;
            termsPanel.NextRequested += () =>
            {
                flow.ContinueToProfile();
                Render();
            };

            signupPanel.NicknameChanged += nickname =>
                EditSignup(form => form.ChangeNickname(nickname));
            signupPanel.AvatarSelected += avatar => EditSignup(form => form.SelectAvatar(avatar));
            signupPanel.BackRequested += () =>
            {
                flow.BackToTerms();
                Render();
            };
            signupPanel.CheckNicknameRequested += () => Run(flow.CheckNicknameAsync);
            signupPanel.SubmitRequested += () => Run(flow.SubmitSignupAsync);

            languagePanel.LanguageChosen += languageCode =>
                Run(() => flow.ChooseLanguageAsync(languageCode));
            ConnectLevelTestPanel();
            levelSettingPanel.LevelConfirmed += level => Run(() => flow.SelectLevelAsync(level));
        }

        // 화면 04: 시작·건너뛰기·그만하기와 문항 [다음]·결과 [다음]은 서버를 부르고, 보기·답·들은 횟수는 테스트 상태만 바꾼다.
        private void ConnectLevelTestPanel()
        {
            levelTestPanel.StartRequested += () => Run(flow.StartLevelTestAsync);
            levelTestPanel.SkipRequested += () => Run(flow.SkipLevelTestAsync);
            levelTestPanel.QuitRequested += () => Run(flow.QuitLevelTestAsync);
            levelTestPanel.NextRequested += () =>
                Run(flow.SubmitLevelTestAnswerAsync, SubmitWaitingMessage());
            levelTestPanel.FinishRequested += () => Run(flow.ContinueAfterLevelTestAsync);
            levelTestPanel.ChoiceSelected += choice => EditLevelTest(test => test.Choose(choice));
            levelTestPanel.ListeningPlayed += () =>
                EditLevelTest(test => test.RecordListeningPlay());
            levelTestPanel.AnswerChanged += answer =>
                EditLevelTest(test => test.ChangeAnswer(answer));
            levelTestPanel.AnswerRecorded += wav =>
                Run(() => flow.TranscribeLevelTestAnswerAsync(wav), TranscribingMessage);
            levelTestPanel.NoticeRaised += notice => messageText.text = notice;
        }

        // 마지막 문항의 [다음]은 말하기 답을 AI가 판정하느라 몇 초 걸려 채점 중임을 알린다.
        private string SubmitWaitingMessage()
        {
            return flow.LevelTest != null && flow.LevelTest.IsLastItem
                ? GradingMessage
                : WaitingMessage;
        }

        private void EditLevelTest(Action<LevelTestSession> edit)
        {
            if (flow.Screen != EntryScreen.LevelTest || flow.LevelTest == null)
            {
                return;
            }

            edit(flow.LevelTest);
            levelTestPanel.Show(flow.LevelTest, flow.IsVoiceAgreed);
        }

        private void ShowGuestPanel()
        {
            guestPanel.Show(flow.GuestAvatar, flow.GuestVoiceConsent, flow.GuestConversationLimit);
        }

        // 캐릭터 선택 화면을 먼저 보여 주고, 대화 횟수(AUTH-08)를 읽으면 안내 문구만 바꾼다. 읽는 동안 버튼을 막지 않는다.
        private async void ShowGuestConversationLimit()
        {
            await flow.LoadGuestGuideAsync();

            // 기다리는 사이 씬을 떠났거나 다른 화면으로 갔으면 그대로 둔다.
            if (this != null && flow.Screen == EntryScreen.GuestCharacter)
            {
                ShowGuestPanel();
            }
        }

        private void EditSignup(Action<SignupForm> edit)
        {
            edit(flow.SignupForm);
            signupPanel.Show(flow.SignupForm);
        }

        private void EditTerms(Action<SignupForm> edit)
        {
            edit(flow.SignupForm);
            termsPanel.Show(flow.SignupForm);
        }

        // 버튼 이벤트에서 부르므로 async void로 두고, 서버 오류가 아닌 예외는 로그로 남긴다.
        // 기다리는 동안 버튼만 흐려지면 멈춘 것처럼 보이므로 아래 안내 문구에 기다리는 중임을 보여 준다.
        private async void Run(Func<Task> action, string waitingMessage = WaitingMessage)
        {
            inputGroup.interactable = false;
            messageText.text = waitingMessage;
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // 기다리는 사이 씬을 떠났으면 이 화면은 이미 사라졌다.
            if (this == null)
            {
                return;
            }

            inputGroup.interactable = true;
            Render();
        }

        // 재접속할 장소가 없거나, 서버가 알려 준 씬이 이 빌드의 Scene List에 없으면 기본 마을로 간다.
        private string WorldSceneName()
        {
            string lastScene = flow.LastSceneName;
            return lastScene != null && Application.CanStreamedLevelBeLoaded(lastScene)
                ? lastScene
                : settings.WorldSceneName;
        }

        private void Render()
        {
            PlayerCharacterSelection.Select(flow.SelectedAvatar?.ToString());
            PlayerNickname.Set(flow.Nickname);
            UiThemeSelection.Shared.Select(UiThemeCodes.Parse(flow.UiTheme));
            VoiceConsent.Shared.Set(flow.IsVoiceAgreed);
            messageText.text = flow.ErrorMessage ?? "";
            backdrop.Show(EntryBackdropView.IsBehindWindow(flow.Screen));
            switch (flow.Screen)
            {
                case EntryScreen.Tutorial:
                    SceneManager.LoadScene(settings.TutorialSceneName);
                    return;
                case EntryScreen.World:
                    // 서버 단계가 튜토리얼 이후면 장소에서 튜토리얼 안내를 다시 띄우지 않는다.
                    TutorialState.MarkFinishedOnServer();
                    SceneManager.LoadScene(WorldSceneName());
                    return;
                case EntryScreen.Login:
                    ShowOnly(loginPanel.gameObject);
                    loginPanel.Show(Application.isEditor || Debug.isDebugBuild, LoginNotice());
                    break;
                case EntryScreen.GuestCharacter:
                    ShowOnly(guestPanel.gameObject);
                    ShowGuestPanel();
                    break;
                case EntryScreen.Terms:
                    ShowOnly(termsPanel.gameObject);
                    termsPanel.Show(flow.SignupForm);
                    break;
                case EntryScreen.Signup:
                    ShowOnly(signupPanel.gameObject);
                    signupPanel.Show(flow.SignupForm);
                    break;
                case EntryScreen.Language:
                    ShowOnly(languagePanel.gameObject);
                    languagePanel.Show(flow.Languages);
                    break;
                case EntryScreen.LevelTest:
                    ShowOnly(levelTestPanel.gameObject);
                    levelTestPanel.Show(flow.LevelTest, flow.IsVoiceAgreed);
                    break;
                case EntryScreen.LevelSetting:
                    ShowOnly(levelSettingPanel.gameObject);
                    levelSettingPanel.Show(flow.LanguageProfile);
                    break;
                case EntryScreen.ConnectionFailed:
                    ShowOnly(connectionPanel.gameObject);
                    break;
                case EntryScreen.GuestReturn:
                    ShowOnly(guestReturnPanel.gameObject);
                    guestReturnPanel.ShowGuestReturn(LoginNotice());
                    break;
                case EntryScreen.ExistingAccount:
                    ShowOnly(guestReturnPanel.gameObject);
                    guestReturnPanel.ShowExistingAccount();
                    break;
            }
        }

        private void ShowOnly(GameObject panel)
        {
            connectionPanel.gameObject.SetActive(connectionPanel.gameObject == panel);
            guestReturnPanel.gameObject.SetActive(guestReturnPanel.gameObject == panel);
            loginPanel.gameObject.SetActive(loginPanel.gameObject == panel);
            guestPanel.gameObject.SetActive(guestPanel.gameObject == panel);
            termsPanel.gameObject.SetActive(termsPanel.gameObject == panel);
            signupPanel.gameObject.SetActive(signupPanel.gameObject == panel);
            languagePanel.gameObject.SetActive(languagePanel.gameObject == panel);
            levelTestPanel.gameObject.SetActive(levelTestPanel.gameObject == panel);
            levelSettingPanel.gameObject.SetActive(levelSettingPanel.gameObject == panel);
        }

        // Google 로그인이 실패하거나 사용자가 동의를 취소하면 서버가 ?loginError=true를 붙여 돌려보낸다(AUTH-02).
        private static string LoginNotice()
        {
            return Application.absoluteURL.Contains(LoginErrorQuery)
                ? "로그인하지 못했어요. 다시 시도해 주세요."
                : "";
        }

        // 게스트 세션에서 이미 가입한 Google 계정으로 로그인하면 서버가 ?existingAccount=true를 붙여 돌려보낸다(AUTH-02).
        private static bool CameFromExistingAccount()
        {
            return Application.absoluteURL.Contains(ExistingAccountQuery);
        }
    }
}
