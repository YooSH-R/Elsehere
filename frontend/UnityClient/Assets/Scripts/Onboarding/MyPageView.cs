using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Player;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 공간 화면의 내 정보 창. 휴대폰 [내 정보]로 열어 닉네임·캐릭터·학습 언어를 보고 닉네임을 바꾼다(USER-04).
    /// 시점 감도·대화 난이도·선택 동의·로그아웃 같은 설정은 <see cref="SettingsView"/>가 맡는다.
    /// 단어장 HUD 프리팹에 붙어 있어 장소 씬마다 따로 배치하지 않는다.
    /// </summary>
    public class MyPageView : MonoBehaviour
    {
        private const string LoadingMessage = "내 정보를 불러오는 중이에요…";
        private const string SavingMessage = "닉네임을 바꾸는 중이에요…";
        private const string UnexpectedMessage = "문제가 생겼어요. 다시 시도해 주세요.";

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        [Tooltip("휴대폰의 [내 정보]")]
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
        private Button closeButton;

        private MyPageFlow flow;
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
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            nicknameButton.onClick.AddListener(ChangeNickname);
            nicknameInput.onSubmit.AddListener(_ => ChangeNickname());
            overlay.SetActive(false);
            UiLayer.Close(this);
            escape = new UiEscapeWindow(() => overlay.activeSelf, Close);
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
            Render();
            await RunAsync(flow.LoadAsync(), LoadingMessage);
        }

        private void Close()
        {
            if (flow.IsBusy)
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
