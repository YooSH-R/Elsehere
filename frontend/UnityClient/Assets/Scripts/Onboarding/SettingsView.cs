using System;
using System.Threading.Tasks;
using A305.Ambience;
using A305.Api;
using A305.Dialogue;
using A305.Player;
using A305.Theme;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 공간 화면의 설정 창. 휴대폰 [설정]으로 열어 시점 회전 감도와 NPC 말 빠르기(<see cref="LookSensitivity"/>,
    /// <see cref="NpcSpeechSpeed"/>), 효과음 켜기·끄기와 크기(<see cref="BackgroundSoundPreference"/>)를 이 기기에 저장하고,
    /// 대화 난이도(ONB-09)와 선택 동의(USER-03)는 서버에 저장한다. 로그아웃과 회원 탈퇴는 <see cref="MyPageView"/>가 맡는다.
    /// 단어장 HUD 프리팹에 붙어 있어 장소 씬마다 따로 배치하지 않는다.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        private const string LoadingMessage = "설정을 불러오는 중이에요…";
        private const string SavingConsentMessage = "동의를 바꾸는 중이에요…";
        private const string SavingLevelMessage = "난이도를 바꾸는 중이에요…";
        private const string UnexpectedMessage = "문제가 생겼어요. 다시 시도해 주세요.";

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        [Tooltip("휴대폰의 [설정]")]
        private Button openButton;

        [SerializeField]
        [Tooltip("화면 전체를 덮는 바탕. 열려 있는 동안 뒤의 HUD와 사물이 눌리지 않는다.")]
        private GameObject overlay;

        [SerializeField]
        private GameObject settingsPanel;

        [SerializeField]
        private TMP_Text statusText;

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
        [Tooltip("대화 난이도 단계 버튼(초급·중급·고급)을 넣을 줄")]
        private Transform levelButtonsParent;

        [SerializeField]
        [Tooltip("대화 난이도 단계 버튼 하나. 받은 단계 수만큼 복제한다.")]
        private Button levelButtonTemplate;

        [SerializeField]
        [Tooltip("효과음 [켜기]. 지금 켜져 있으면 강조색으로 칠한다.")]
        private Button soundOnButton;

        [SerializeField]
        [Tooltip("효과음 [끄기]. 지금 꺼져 있으면 강조색으로 칠한다.")]
        private Button soundOffButton;

        [SerializeField]
        [Tooltip("효과음 크기(0~1). 0으로 내리면 끄고, 올리면 그 크기로 켠다.")]
        private Slider soundVolumeSlider;

        [SerializeField]
        [Tooltip("막대 옆에 지금 효과음 크기를 퍼센트로 보여 준다. 꺼져 있으면 0%다.")]
        private TMP_Text soundVolumeText;

        [SerializeField]
        [Tooltip("선택 동의(음성 처리·AI 개선) 줄을 넣을 곳")]
        private Transform consentsParent;

        [SerializeField]
        [Tooltip("선택 동의 한 줄. 받은 동의 수만큼 복제한다.")]
        private ConsentRowView consentTemplate;

        [SerializeField]
        private Button closeButton;

        private MyPageFlow flow;
        private PlayerMover player;
        private UiEscapeWindow escape;

        private void Awake()
        {
            // 높이는 내용(ContentSizeFitter)에 맞추므로 가로만 바꾼다. 칸은 세로 배치(VerticalLayoutGroup)가 넓힌다.
            WindowFrame.Attach(
                (RectTransform)settingsPanel.transform,
                minimumWidth: .75f,
                resizesHeight: false
            );
            if (apiSettings == null)
            {
                Debug.LogError("SettingsView의 apiSettings가 연결되지 않았습니다.", this);
                openButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            flow = new MyPageFlow(new OnboardingApi(apiSettings.CreateClient()));
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            soundOnButton.onClick.AddListener(() => ChangeSound(true));
            soundOffButton.onClick.AddListener(() => ChangeSound(false));
            consentTemplate.gameObject.SetActive(false);
            levelButtonTemplate.gameObject.SetActive(false);
            lookSensitivitySlider.minValue = LookSensitivity.Min;
            lookSensitivitySlider.maxValue = LookSensitivity.Max;
            lookSensitivitySlider.onValueChanged.AddListener(ChangeLookSensitivity);
            npcSpeechSpeedSlider.minValue = NpcSpeechSpeed.Min;
            npcSpeechSpeedSlider.maxValue = NpcSpeechSpeed.Max;
            npcSpeechSpeedSlider.onValueChanged.AddListener(ChangeNpcSpeechSpeed);
            soundVolumeSlider.minValue = 0;
            soundVolumeSlider.maxValue = 1;
            soundVolumeSlider.onValueChanged.AddListener(ChangeSoundVolume);
            overlay.SetActive(false);
            UiLayer.Close(this);
            escape = new UiEscapeWindow(() => overlay.activeSelf, Close);
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
            lookSensitivitySlider.SetValueWithoutNotify(LookSensitivity.Current);
            RenderLookSensitivity(LookSensitivity.Current);
            npcSpeechSpeedSlider.SetValueWithoutNotify(NpcSpeechSpeed.Current);
            RenderNpcSpeechSpeed(NpcSpeechSpeed.Current);
            Render();
            await RunAsync(flow.LoadAsync(), LoadingMessage);
            // 게스트 닉네임 안내처럼 내 정보 창에 해당하는 안내는 여기서 보이지 않는다.
            if (this != null && flow.User != null)
            {
                statusText.text = "";
            }
            ApplyVoiceConsent();
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

        private async void ChangeConsent(string consentType, bool agreed)
        {
            if (flow.IsBusy)
            {
                return;
            }

            await RunAsync(flow.ChangeConsentAsync(consentType, agreed), SavingConsentMessage);
            ApplyVoiceConsent();
        }

        // 진행 중인 대화는 시작할 때의 난이도를 그대로 쓰고, 바꾼 난이도는 다음 대화부터 서버가 적용한다.
        private async void ChangeLevel(int level)
        {
            if (flow.IsBusy)
            {
                return;
            }

            await RunAsync(flow.ChangeLevelAsync(level), SavingLevelMessage);
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

        // 생활소음·효과음·발자국·접속 음악이 바로 따른다. NPC 음성은 바뀌지 않는다.
        private void ChangeSound(bool enabled)
        {
            BackgroundSoundPreference.Enabled = enabled;
            RenderSound();
        }

        // 5% 단위로 저장한다. 0까지 내리면 끄되 전에 고른 크기는 남겨, [켜기]를 누르면 그 크기로 돌아온다.
        private void ChangeSoundVolume(float value)
        {
            BackgroundSoundPreference.SetLevel(Mathf.Round(value * 20f) / 20f);
            RenderSound();
        }

        // 꺼져 있으면 막대를 0%로 보여 [끄기]와 막대가 같은 상태를 가리키게 한다.
        private void RenderSound()
        {
            bool enabled = BackgroundSoundPreference.Enabled;
            float level = BackgroundSoundPreference.Level;
            soundVolumeSlider.SetValueWithoutNotify(level);
            soundVolumeText.text = $"{level * 100f:0}%";
            soundOnButton.GetComponent<ThemedElement>().SetSelected(enabled);
            soundOffButton.GetComponent<ThemedElement>().SetSelected(!enabled);
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
            statusText.text = flow.Message ?? "";
            closeButton.interactable = !flow.IsBusy;
            RenderSound();
            RenderConsents(canEdit);
            RenderLevels(canEdit);
        }

        // 지금 난이도 버튼을 강조색으로 칠한다. 고를 수 없는 단계(고급)는 흐리게 막는다.
        // 실패하면 흐름의 난이도가 바뀌지 않으므로 다시 그리면 원래 버튼으로 돌아간다.
        private void RenderLevels(bool canEdit)
        {
            TemplateClones.Clear(levelButtonsParent, levelButtonTemplate);
            foreach (ConversationLevel level in flow.Levels)
            {
                Button button = TemplateClones.Add(levelButtonTemplate, levelButtonsParent);
                button.GetComponentInChildren<TMP_Text>(true).text = level.Name;
                button.interactable = canEdit && level.Available;
                button.GetComponent<ThemedElement>().SetSelected(level.Level == flow.SelectedLevel);
                button.onClick.AddListener(() => ChangeLevel(level.Level));
            }
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

        private void SetMovementLocked(bool isLocked)
        {
            if (player != null)
            {
                player.SetMovementLocked(isLocked);
            }
        }
    }
}
