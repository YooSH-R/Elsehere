using System;
using System.Threading.Tasks;
using A305.Api;
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
        private SignupPanelView signupPanel;

        [SerializeField]
        private LanguagePanelView languagePanel;

        [SerializeField]
        private LevelTestPanelView levelTestPanel;

        [SerializeField]
        private LevelSettingPanelView levelSettingPanel;

        private IOnboardingApi api;
        private OnboardingFlow flow;

        private void Awake()
        {
            api = new OnboardingApi(apiSettings.CreateClient());
            flow = new OnboardingFlow(api);
            ConnectPanels();
            ShowOnly(null);
            messageText.text = "접속 상태를 확인하고 있어요.";
        }

        private void Start()
        {
            Run(flow.StartAsync);
        }

        private void ConnectPanels()
        {
            loginPanel.GoogleLoginRequested += () => BrowserRedirect.GoTo(api.GoogleLoginUrl);
            loginPanel.DevLoginRequested += () =>
                Run(() => flow.DevLoginAsync(settings.DevLoginUserId));

            signupPanel.NicknameChanged += nickname =>
                EditSignup(form => form.ChangeNickname(nickname));
            signupPanel.AvatarSelected += avatar => EditSignup(form => form.SelectAvatar(avatar));
            signupPanel.ConsentChanged += (consentType, agreed) =>
                EditSignup(form => form.SetAgreed(consentType, agreed));
            signupPanel.ConsentDocumentRequested += Application.OpenURL;
            signupPanel.CheckNicknameRequested += () => Run(flow.CheckNicknameAsync);
            signupPanel.SubmitRequested += () => Run(flow.SubmitSignupAsync);

            languagePanel.LanguageChosen += languageCode =>
                Run(() => flow.ChooseLanguageAsync(languageCode));
            levelTestPanel.SkipRequested += () => Run(flow.SkipLevelTestAsync);
            levelSettingPanel.LevelConfirmed += level => Run(() => flow.SelectLevelAsync(level));
        }

        private void EditSignup(Action<SignupForm> edit)
        {
            edit(flow.SignupForm);
            signupPanel.Show(flow.SignupForm);
        }

        // 버튼 이벤트에서 부르므로 async void로 두고, 서버 오류가 아닌 예외는 로그로 남긴다.
        private async void Run(Func<Task> action)
        {
            inputGroup.interactable = false;
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

        private void Render()
        {
            messageText.text = flow.ErrorMessage ?? "";
            switch (flow.Screen)
            {
                case EntryScreen.Tutorial:
                    SceneManager.LoadScene(settings.TutorialSceneName);
                    return;
                case EntryScreen.World:
                    SceneManager.LoadScene(settings.WorldSceneName);
                    return;
                case EntryScreen.Login:
                    ShowOnly(loginPanel.gameObject);
                    loginPanel.Show(Application.isEditor || Debug.isDebugBuild, LoginNotice());
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
                    break;
                case EntryScreen.LevelSetting:
                    ShowOnly(levelSettingPanel.gameObject);
                    levelSettingPanel.Show(flow.LanguageProfile);
                    break;
            }
        }

        private void ShowOnly(GameObject panel)
        {
            loginPanel.gameObject.SetActive(loginPanel.gameObject == panel);
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
    }
}
