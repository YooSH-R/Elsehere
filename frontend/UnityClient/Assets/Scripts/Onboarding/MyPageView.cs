using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 공간 화면의 마이페이지. 오른쪽 위 [내 정보]로 열어 내 정보를 보고 닉네임을 바꾸거나 로그아웃한다.
    /// 시점 회전 감도도 여기서 조절한다(<see cref="LookSensitivity"/>, 이 기기에만 저장).
    /// 로그아웃은 확인 창을 거쳐 서버 세션을 끝낸 뒤(AUTH-05) 입구 씬의 로그인 화면으로 돌아간다.
    /// 단어장 HUD 프리팹에 붙어 있어 장소 씬마다 따로 배치하지 않는다.
    /// </summary>
    public class MyPageView : MonoBehaviour
    {
        private const string LoadingMessage = "내 정보를 불러오는 중이에요…";
        private const string SavingMessage = "닉네임을 바꾸는 중이에요…";
        private const string LoggingOutMessage = "로그아웃하는 중이에요…";
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
        [Tooltip("시점 회전 감도 배율. 이 기기에만 저장한다.")]
        private Slider lookSensitivitySlider;

        [SerializeField]
        [Tooltip("슬라이더 옆에 지금 감도 배율을 보여 준다. 슬라이더를 움직이는 동안에도 바뀐다.")]
        private TMP_Text lookSensitivityText;

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

        private MyPageFlow flow;
        private LogoutFlow logout;
        private PlayerMover player;

        private void Awake()
        {
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
            lookSensitivitySlider.minValue = LookSensitivity.Min;
            lookSensitivitySlider.maxValue = LookSensitivity.Max;
            lookSensitivitySlider.onValueChanged.AddListener(ChangeLookSensitivity);
            overlay.SetActive(false);
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerMover>();
        }

        // 대화처럼 다른 화면이 이동을 잠근 동안에는 열지 않는다. 닫을 때 그 잠금을 풀어 버리지 않기 위함이다.
        private void Update()
        {
            openButton.interactable =
                overlay.activeSelf || player == null || !player.IsMovementLocked;
        }

        // 버튼 이벤트에서 부르므로 async void로 두고, 서버 오류가 아닌 예외는 로그로 남긴다.
        private async void Open()
        {
            if (overlay.activeSelf)
            {
                return;
            }

            overlay.SetActive(true);
            SetMovementLocked(true);
            nicknameInput.text = "";
            lookSensitivitySlider.SetValueWithoutNotify(LookSensitivity.Current);
            RenderLookSensitivity(LookSensitivity.Current);
            ShowProfile();
            await RunAsync(flow.LoadAsync(), LoadingMessage);
        }

        private void Close()
        {
            if (flow.IsBusy || logout.IsBusy)
            {
                return;
            }

            overlay.SetActive(false);
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
            profileText.text = flow.User != null ? MyPageText.Profile(flow.User) : "";
            statusText.text = flow.Message ?? "";
            nicknameInput.interactable = canEdit;
            nicknameButton.interactable = canEdit;
            closeButton.interactable = !flow.IsBusy;
            logoutButton.interactable = !flow.IsBusy;
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
