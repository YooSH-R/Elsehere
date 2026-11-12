using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Player;
using A305.Theme;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 공간 화면의 마이페이지. 오른쪽 위 [내 정보]로 열어 내 정보를 보고 닉네임을 바꾸거나 로그아웃한다.
    /// 시점 회전 감도와 NPC 말 빠르기도 여기서 조절한다(<see cref="LookSensitivity"/>, <see cref="NpcSpeechSpeed"/>, 이 기기에만 저장).
    /// 로그아웃은 확인 창을 거쳐 서버 세션을 끝낸 뒤(AUTH-05) 입구 씬의 로그인 화면으로 돌아간다.
    /// 단어장 HUD 프리팹에 붙어 있어 장소 씬마다 따로 배치하지 않는다.
    /// </summary>
    public class MyPageView : MonoBehaviour
    {
        private const string LoadingMessage = "내 정보를 불러오는 중이에요…";
        private const string SavingMessage = "닉네임을 바꾸는 중이에요…";
        private const string SavingConsentMessage = "동의를 바꾸는 중이에요…";
        private const string LoggingOutMessage = "로그아웃하는 중이에요…";
        private const string WithdrawingMessage = "탈퇴하는 중이에요…";
        private const string UnexpectedMessage = "문제가 생겼어요. 다시 시도해 주세요.";

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        [Tooltip("로그아웃한 뒤 여는 입구 씬")]
        private string entrySceneName = "EntryScene";

        [SerializeField]
        [Tooltip("오른쪽 위 [단어장] 옆의 [내 정보]")]
        private Button openButton;

        [SerializeField]
        [Tooltip("화면 전체를 덮는 바탕. 열려 있는 동안 뒤의 HUD와 사물이 눌리지 않는다.")]
        private GameObject overlay;

        [SerializeField]
        private GameObject profilePanel;

        [SerializeField]
        private TMP_Text profileText;

        [SerializeField]
        private TMP_InputField nicknameInput;

        [SerializeField]
        private Button nicknameButton;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        [Tooltip("선택 동의(음성 처리·AI 개선) 줄을 넣을 곳")]
        private Transform consentsParent;

        [SerializeField]
        [Tooltip("선택 동의 한 줄. 받은 동의 수만큼 복제한다.")]
        private ConsentRowView consentTemplate;

        [SerializeField]
        [Tooltip("시점 회전 감도 배율. 이 기기에만 저장한다.")]
        private Slider lookSensitivitySlider;

        [SerializeField]
        [Tooltip("슬라이더 옆에 지금 감도 배율을 보여 준다. 슬라이더를 움직이는 동안에도 바뀐다.")]
        private TMP_Text lookSensitivityText;

        [SerializeField]
        [Tooltip("NPC 대화 음성 빠르기 배율. 이 기기에만 저장하고 다음 NPC 대사부터 쓴다.")]
        private Slider npcSpeechSpeedSlider;

        [SerializeField]
        [Tooltip(
            "슬라이더 옆에 지금 말 빠르기 배율을 보여 준다. 슬라이더를 움직이는 동안에도 바뀐다."
        )]
        private TMP_Text npcSpeechSpeedText;

        [SerializeField]
        private Button logoutButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private GameObject logoutDialog;

        [SerializeField]
        private TMP_Text logoutStatusText;

        [SerializeField]
        private Button logoutConfirmButton;

        [SerializeField]
        private Button logoutCancelButton;

        [SerializeField]
        [Tooltip("[회원 탈퇴](USER-05). 확인 창을 거친다.")]
        private Button withdrawButton;

        [SerializeField]
        private GameObject withdrawDialog;

        [SerializeField]
        private TMP_Text withdrawStatusText;

        [SerializeField]
        private Button withdrawConfirmButton;

        [SerializeField]
        private Button withdrawCancelButton;

        private MyPageFlow flow;
        private LogoutFlow logout;
        private PlayerMover player;

        private void Awake()
        {
            // 높이는 내용(ContentSizeFitter)에 맞추므로 가로만 바꾼다. 칸은 세로 배치(VerticalLayoutGroup)가 넓힌다.
            WindowFrame.Attach(
                (RectTransform)profilePanel.transform,
                minimumWidth: .75f,
                resizesHeight: false
            );
            if (apiSettings == null)
            {
                Debug.LogError("MyPageView의 apiSettings가 연결되지 않았습니다.", this);
                openButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            var api = new OnboardingApi(apiSettings.CreateClient());
            flow = new MyPageFlow(api);
            logout = new LogoutFlow(api);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            nicknameButton.onClick.AddListener(ChangeNickname);
            nicknameInput.onSubmit.AddListener(_ => ChangeNickname());
            logoutButton.onClick.AddListener(AskLogout);
            logoutCancelButton.onClick.AddListener(ShowProfile);
            logoutConfirmButton.onClick.AddListener(Logout);
            withdrawButton.onClick.AddListener(AskWithdraw);
            withdrawCancelButton.onClick.AddListener(ShowProfile);
            withdrawConfirmButton.onClick.AddListener(Withdraw);
            consentTemplate.gameObject.SetActive(false);
            lookSensitivitySlider.minValue = LookSensitivity.Min;
            lookSensitivitySlider.maxValue = LookSensitivity.Max;
            lookSensitivitySlider.onValueChanged.AddListener(ChangeLookSensitivity);
            npcSpeechSpeedSlider.minValue = NpcSpeechSpeed.Min;
            npcSpeechSpeedSlider.maxValue = NpcSpeechSpeed.Max;
            npcSpeechSpeedSlider.onValueChanged.AddListener(ChangeNpcSpeechSpeed);
            overlay.SetActive(false);
            UiLayer.Close(this);
            // ESC는 로그아웃·탈퇴 확인이면 프로필로 돌아가고, 프로필이면 창을 닫는다. 요청 중에는 닫기 버튼처럼 닫지 않는다.
            escape = new UiEscapeWindow(
                () => overlay.activeSelf,
                () =>
                {
                    if (logoutDialog.activeSelf || withdrawDialog.activeSelf)
                    {
                        if (!flow.IsBusy && !logout.IsBusy)
                            ShowProfile();
                    }
                    else
                    {
                        Close();
                    }
                }
            );
        }

        private UiEscapeWindow escape;

        private void Start()
        {
            player = FindFirstObjectByType<PlayerMover>();
        }

        // 대화처럼 다른 화면이 이동을 잠근 동안에는 열지 않는다. 닫을 때 그 잠금을 풀어 버리지 않기 위함이다.
        private void Update()
        {
            openButton.interactable =
                overlay.activeSelf || player == null || !player.IsMovementLocked;
            escape?.Watch();
        }

        // 버튼 이벤트에서 부르므로 async void로 두고, 서버 오류가 아닌 예외는 로그로 남긴다.
        private async void Open()
        {
            if (overlay.activeSelf)
            {
                return;
            }

            overlay.SetActive(true);
            UiLayer.Open(this);
            SetMovementLocked(true);
            nicknameInput.text = "";
            lookSensitivitySlider.SetValueWithoutNotify(LookSensitivity.Current);
            RenderLookSensitivity(LookSensitivity.Current);
            npcSpeechSpeedSlider.SetValueWithoutNotify(NpcSpeechSpeed.Current);
            RenderNpcSpeechSpeed(NpcSpeechSpeed.Current);
            ShowProfile();
            await RunAsync(flow.LoadAsync(), LoadingMessage);
            ApplyVoiceConsent();
        }

        private void Close()
        {
            if (flow.IsBusy || logout.IsBusy)
            {
                return;
            }

            overlay.SetActive(false);
            UiLayer.Close(this);
            SetMovementLocked(false);
        }

        private async void ChangeNickname()
        {
            if (flow.IsBusy)
            {
                return;
            }

            Task<bool> changing = flow.ChangeNicknameAsync(nicknameInput.text);
            await RunAsync(changing, SavingMessage);
            if (this != null && changing.IsCompletedSuccessfully && changing.Result)
            {
                nicknameInput.text = "";
            }
        }

        private async void ChangeConsent(string consentType, bool agreed)
        {
            if (flow.IsBusy)
            {
                return;
            }

            await RunAsync(flow.ChangeConsentAsync(consentType, agreed), SavingConsentMessage);
            ApplyVoiceConsent();
        }

        // 대화의 [말하기]가 바뀐 음성 처리 동의를 따르게 한다.
        private void ApplyVoiceConsent()
        {
            if (this != null && flow.User != null)
            {
                VoiceConsent.Shared.Set(flow.IsVoiceAgreed);
            }
        }

        // 움직이는 대로 바로 저장해 창을 닫지 않아도 다음 회전부터 적용된다.
        private void ChangeLookSensitivity(float value)
        {
            LookSensitivity.Current = Mathf.Round(value * 10f) / 10f;
            RenderLookSensitivity(LookSensitivity.Current);
        }

        private void RenderLookSensitivity(float value)
        {
            lookSensitivityText.text = $"{value:0.0}배";
        }

        // 서버가 음성을 만들 때 쓰므로 바로 저장해 다음 대화 요청부터 실린다.
        private void ChangeNpcSpeechSpeed(float value)
        {
            NpcSpeechSpeed.Current = value;
            RenderNpcSpeechSpeed(NpcSpeechSpeed.Current);
        }

        private void RenderNpcSpeechSpeed(float value)
        {
            npcSpeechSpeedText.text = $"{value:0.0}배";
        }

        private void AskLogout()
        {
            profilePanel.SetActive(false);
            logoutDialog.SetActive(true);
            logoutStatusText.text = "";
            SetLogoutButtonsInteractable(true);
        }

        private void ShowProfile()
        {
            logoutDialog.SetActive(false);
            withdrawDialog.SetActive(false);
            profilePanel.SetActive(true);
            Render();
        }

        private async void Logout()
        {
            SetLogoutButtonsInteractable(false);
            logoutStatusText.text = LoggingOutMessage;
            bool canLeave;
            try
            {
                canLeave = await logout.LogoutAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                canLeave = false;
            }

            if (this == null)
            {
                return;
            }

            if (canLeave)
            {
                SceneManager.LoadScene(entrySceneName);
                return;
            }

            logoutStatusText.text = logout.ErrorMessage ?? UnexpectedMessage;
            SetLogoutButtonsInteractable(true);
        }

        private void AskWithdraw()
        {
            profilePanel.SetActive(false);
            withdrawDialog.SetActive(true);
            withdrawStatusText.text = "";
            SetWithdrawButtonsInteractable(true);
        }

        // 탈퇴하면 서버 세션도 끝나므로 로그아웃처럼 입구 씬의 로그인 화면으로 돌아간다.
        private async void Withdraw()
        {
            SetWithdrawButtonsInteractable(false);
            withdrawStatusText.text = WithdrawingMessage;
            bool canLeave;
            try
            {
                canLeave = await logout.WithdrawAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                canLeave = false;
            }

            if (this == null)
            {
                return;
            }

            if (canLeave)
            {
                SceneManager.LoadScene(entrySceneName);
                return;
            }

            withdrawStatusText.text = logout.ErrorMessage ?? UnexpectedMessage;
            SetWithdrawButtonsInteractable(true);
        }

        private void SetWithdrawButtonsInteractable(bool interactable)
        {
            withdrawConfirmButton.interactable = interactable;
            withdrawCancelButton.interactable = interactable;
        }

        // 요청을 기다리는 동안 버튼을 막고 기다리는 중임을 보이고, 끝나면 결과를 다시 그린다.
        private async Task RunAsync(Task request, string waitingMessage)
        {
            Render();
            statusText.text = waitingMessage;
            string failure = null;
            try
            {
                await request;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                failure = UnexpectedMessage;
            }

            if (this == null)
            {
                return;
            }

            Render();
            if (failure != null)
            {
                statusText.text = failure;
            }
        }

        private void Render()
        {
            bool canEdit = flow.User != null && !flow.IsBusy;
            profileText.text = flow.User != null ? MyPageText.ProfileForDisplay(flow.User) : "";
            statusText.text = flow.Message ?? "";
            // 게스트는 닉네임을 바꾸지 못해 칸과 버튼을 막고, 이유는 아래 안내 줄에 보인다.
            bool canChangeNickname = canEdit && flow.CanChangeNickname;
            nicknameInput.interactable = canChangeNickname;
            nicknameButton.interactable = canChangeNickname;
            closeButton.interactable = !flow.IsBusy;
            logoutButton.interactable = !flow.IsBusy;
            withdrawButton.gameObject.SetActive(flow.CanWithdraw);
            withdrawButton.interactable = !flow.IsBusy;
            RenderConsents(canEdit);
        }

        // 실패하면 흐름의 동의가 바뀌지 않으므로 다시 그리면 토글이 원래 상태로 돌아간다.
        private void RenderConsents(bool canEdit)
        {
            TemplateClones.Clear(consentsParent, consentTemplate);
            foreach (ConsentStatus consent in flow.Consents)
            {
                ConsentRowView row = TemplateClones.Add(consentTemplate, consentsParent);
                row.Show(
                    new ConsentTerm
                    {
                        ConsentType = consent.ConsentType,
                        Title = consent.Title,
                        Url = consent.Url,
                    },
                    showRequirement: false
                );
                row.Toggle.SetIsOnWithoutNotify(consent.Agreed);
                row.Toggle.interactable = canEdit;
                row.Toggle.onValueChanged.AddListener(agreed =>
                    ChangeConsent(consent.ConsentType, agreed)
                );
                row.ViewButton.onClick.AddListener(() => Application.OpenURL(consent.Url));
            }
        }

        private void SetLogoutButtonsInteractable(bool interactable)
        {
            logoutConfirmButton.interactable = interactable;
            logoutCancelButton.interactable = interactable;
        }

        private void SetMovementLocked(bool isLocked)
        {
            if (player != null)
            {
                player.SetMovementLocked(isLocked);
            }
        }
    }
}
