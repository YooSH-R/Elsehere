using System;
using A305.Theme;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 화면. 이번 대화의 대사, 안내·상태 문구, 추천 단어, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[표현 저장]·[대화 종료],
    /// 서버 AI 대화의 [꾹 눌러 말하기]·[다시 듣기]·[힌트]·[다시 시도]가 있다.
    /// </summary>
    public class DialoguePanelView : MonoBehaviour
    {
        private const string FreeTalkGuide =
            "정답은 없어요. 영어로 자유롭게 말하거나 입력해 보세요. 한국어를 섞어도 돼요.";
        private const string TalkCaption = "꾹 눌러 말하기";
        private const string ListeningCaption = "듣는 중…";

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text npcNameText;

        [SerializeField]
        [Tooltip("NPC 이름 옆의 호감도와 무례 경고 칸. 서버가 보내지 않으면 숨긴다.")]
        private TMP_Text standingText;

        [SerializeField]
        private Button endButton;

        [SerializeField]
        private TMP_Text linesText;

        [SerializeField]
        private TMP_Text guideText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private TMP_Text suggestText;

        [SerializeField]
        private RectTransform helpPanel;

        [SerializeField]
        private TMP_Text hintText;

        [SerializeField]
        private Button useHintButton;

        [SerializeField]
        private ScrollRect historyScroll;
        private Vector2 lastCanvasSize;

        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private Button exampleButton;

        [SerializeField]
        private HoldButton talkButton;

        [SerializeField]
        private Button meaningButton;

        [SerializeField]
        private Button saveButton;

        [SerializeField]
        private Button replayButton;

        [SerializeField]
        private Button hintButton;

        [SerializeField]
        private Button retryButton;

        private IDialogueContent content;
        private bool isMeaningVisible;
        private ConversationStandingDisplay standingDisplay = new ConversationStandingDisplay();
        private TMP_Text talkLabel;
        private ThemedElement sendTheme;

        /// <summary>[대화 종료]를 누르면 알린다.</summary>
        public event Action EndRequested;

        /// <summary>[보내기]나 Enter로 입력한 문장을 알린다.</summary>
        public event Action<string> SendRequested;

        /// <summary>[표현 저장]을 누르면 알린다. 저장할 대사는 대화 처리가 정한다.</summary>
        public event Action SaveRequested;

        /// <summary>[꾹 눌러 말하기]를 누르기 시작하면 알린다.</summary>
        public event Action TalkPressed;

        /// <summary>[꾹 눌러 말하기]에서 손을 떼면 알린다.</summary>
        public event Action TalkReleased;

        /// <summary>[다시 듣기]를 누르면 알린다.</summary>
        public event Action ReplayRequested;

        /// <summary>[뜻 보기]로 뜻을 펼쳤는데 아직 뜻이 없으면 알린다. 서버 대화는 이때 뜻을 받아 온다.</summary>
        public event Action MeaningRequested;

        /// <summary>[힌트]를 누르면 알린다.</summary>
        public event Action HintRequested;

        /// <summary>[다시 시도]를 누르면 알린다.</summary>
        public event Action RetryRequested;

        public bool IsVisible => panel.activeSelf;

        /// <summary>[표현 저장]을 보여 줄지. 저장할 곳이 없으면 끈다.</summary>
        public bool IsSaveAvailable { get; set; } = true;

        public void Show(IDialogueContent shownContent)
        {
            content = shownContent;
            isMeaningVisible = false;
            standingDisplay = new ConversationStandingDisplay();
            inputField.text = "";
            panel.SetActive(true);
            helpPanel.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            content = null;
            if (talkLabel != null)
            {
                talkLabel.text = TalkCaption;
            }

            panel.SetActive(false);
            helpPanel.gameObject.SetActive(false);
        }

        /// <summary>전사한 문장처럼 입력창에 넣어 줄 문장. 사용자가 확인하고 [보내기]로 보낸다.</summary>
        public void SetInputText(string text)
        {
            inputField.text = text;
        }

        /// <summary>대화 내용이 바뀐 뒤 화면을 다시 그린다.</summary>
        public void Refresh()
        {
            if (content == null)
            {
                return;
            }

            // 보내지 못한 문장은 입력창에 되돌려 놓아 그대로 다시 보낼 수 있게 한다.
            if (content is ServerConversation server && string.IsNullOrEmpty(inputField.text))
            {
                string failed = server.TakeTextToRestore();
                if (!string.IsNullOrEmpty(failed))
                {
                    inputField.text = failed;
                }
            }

            linesText.text = DialogueLinesText.Format(
                content.Lines,
                content.NpcName,
                isMeaningVisible,
                content.LastNpcGlossary
            );
            npcNameText.text = content.NpcName;
            UpdateStanding();
            guideText.text = FormatGuide();
            guideText.gameObject.SetActive(content.IsGuided);
            statusText.text = FormatStatus();
            // 녹음 중·전사 뒤처럼 바쁘지 않아도 할 일을 알리는 안내가 있으면 보인다.
            // 음성 처리에 동의하지 않아 [꾹 눌러 말하기]가 없으면, 버튼이 왜 없는지와 동의를 켜는 곳을 입력칸 위에 알린다.
            statusText.gameObject.SetActive(
                content.IsBusy
                    || content.CanRetry
                    || content.IsGuidedCompleted
                    || NeedsVoiceConsent
                    || !string.IsNullOrEmpty(content.StatusMessage)
            );
            suggestText.text = DialogueHelpText.Words(content.SuggestWords);
            string sentence = DialogueHelpText.Sentence(content.HintText, content.ExampleSentence);
            hintText.text = string.IsNullOrEmpty(sentence)
                ? "[힌트]를 누르면 추천 문장을 볼 수 있어요."
                : sentence;
            useHintButton.interactable = content.CanSend && !string.IsNullOrEmpty(sentence);

            sendButton.interactable = content.CanSend;
            // 시작에 실패했거나 끝난 대화에 입력할 수 있으면 [보내기]가 아무 일도 하지 않는 것처럼 보이므로 함께 막는다.
            inputField.interactable = content.CanSend;
            HighlightSend();
            exampleButton.gameObject.SetActive(!string.IsNullOrEmpty(content.ExampleSentence));
            meaningButton.gameObject.SetActive(content.HasMeaning);
            meaningButton.interactable = content.LastNpcLine != null;
            saveButton.gameObject.SetActive(IsSaveAvailable);
            saveButton.interactable = content.LastNpcLine != null;
            talkButton.gameObject.SetActive(content.UsesVoice && content.CanTalk);
            talkButton.GetComponent<Button>().interactable = content.CanSend;
            replayButton.gameObject.SetActive(content.CanReplaySpeech);
            replayButton.interactable = content.LastNpcLine != null && !content.IsBusy;
            hintButton.gameObject.SetActive(content.HasHint);
            hintButton.interactable =
                content.LastNpcLine != null && content.CanSend && content.HintText == null;
            retryButton.gameObject.SetActive(content.CanRetry);
            UpdateLayout();
            Canvas.ForceUpdateCanvases();
            // 대사가 쌓이면 가장 최근 대사와 뜻이 보이게 맨 아래로 내린다. 지난 대사는 위로 스크롤해 다시 본다.
            historyScroll.verticalNormalizedPosition = 0;
        }

        private void Awake()
        {
            endButton.onClick.AddListener(() => EndRequested?.Invoke());
            sendButton.onClick.AddListener(RequestSend);
            sendButton.TryGetComponent(out sendTheme);
            inputField.onSubmit.AddListener(_ => RequestSend());
            inputField.onValueChanged.AddListener(_ => HighlightSend());
            exampleButton.onClick.AddListener(FillExampleSentence);
            useHintButton.onClick.AddListener(() =>
            {
                if (content != null && content.CanSend)
                    inputField.text =
                        DialogueHelpText.Sentence(content.HintText, content.ExampleSentence) ?? "";
            });
            meaningButton.onClick.AddListener(ToggleMeaning);
            saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
            replayButton.onClick.AddListener(() => ReplayRequested?.Invoke());
            hintButton.onClick.AddListener(() => HintRequested?.Invoke());
            retryButton.onClick.AddListener(RequestRetry);
            talkLabel = talkButton.transform.Find("Label").GetComponent<TMP_Text>();
            talkButton.Pressed += StartTalking;
            talkButton.Released += StopTalking;
            Hide();
        }

        // 녹음은 보낼 수 있을 때만 시작되므로, 그때만 버튼에 듣고 있다고 보인다.
        private void StartTalking()
        {
            if (content != null && content.CanSend)
            {
                talkLabel.text = ListeningCaption;
            }

            TalkPressed?.Invoke();
        }

        private void StopTalking()
        {
            talkLabel.text = TalkCaption;
            TalkReleased?.Invoke();
        }

        // 들은 문장이나 입력한 문장이 있으면 [보내기]를 강조해 다음에 누를 곳을 알린다.
        private void HighlightSend()
        {
            if (sendTheme != null)
            {
                sendTheme.SetSelected(
                    content != null
                        && content.CanSend
                        && !string.IsNullOrWhiteSpace(inputField.text)
                );
            }
        }

        // 입력창에 되돌려 놓은 실패 문장은 [다시 시도]가 그대로 다시 보내므로 비운다.
        private void RequestRetry()
        {
            inputField.text = "";
            RetryRequested?.Invoke();
        }

        private string FormatStatus()
        {
            if (string.IsNullOrEmpty(content.RecoveryGuide))
            {
                // 녹음·오류 같은 지금 상황 안내가 먼저이고, 없을 때만 음성 동의 안내를 보인다.
                return string.IsNullOrEmpty(content.StatusMessage) && NeedsVoiceConsent
                    ? VoiceInputNotice.ConsentRequired
                    : content.StatusMessage ?? "";
            }

            return string.IsNullOrEmpty(content.StatusMessage)
                ? content.RecoveryGuide
                : $"{content.StatusMessage}\n{content.RecoveryGuide}";
        }

        private void RequestSend()
        {
            if (content == null || !content.CanSend)
            {
                return;
            }

            string text = inputField.text;
            inputField.text = "";
            isMeaningVisible = false;
            SendRequested?.Invoke(text);
        }

        private void FillExampleSentence()
        {
            inputField.text = content.ExampleSentence;
        }

        private void ToggleMeaning()
        {
            isMeaningVisible = !isMeaningVisible;
            if (isMeaningVisible && content.LastNpcLine?.Korean == null)
            {
                MeaningRequested?.Invoke();
            }

            Refresh();
        }

        private void LateUpdate()
        {
            if (content == null)
                return;
            // 변화량과 경고 문구는 몇 초 뒤 새 상태가 없어도 사라져야 하므로 매 프레임 다시 고른다.
            UpdateStanding();
            if (((RectTransform)panel.transform.parent).rect.size != lastCanvasSize)
                UpdateLayout();
        }

        // 서버 대화의 호감도·무례 경고 칸. 대본 대화이거나 서버가 보내지 않으면 숨긴다. 색은 테마의 NPC 이름 색을 따른다.
        private void UpdateStanding()
        {
            string text = standingDisplay.Text(content.Standing, Time.unscaledTime);
            standingText.gameObject.SetActive(text != null);
            if (text == null || standingText.text == text)
                return;
            standingText.text = text;
            standingText.color = npcNameText.color;
        }

        private bool vehicleLayout;

        [SerializeField]
        private TMP_Text vehicleStatusText;

        public void SetVehicleLayout(bool enabled)
        {
            vehicleLayout = enabled;
            if (!enabled && vehicleStatusText != null)
                vehicleStatusText.transform.parent.gameObject.SetActive(false);
            UpdateLayout();
        }

        public void SetVehicleStatus(string message)
        {
            if (vehicleStatusText == null)
                return;
            vehicleStatusText.text = message;
            vehicleStatusText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void UpdateLayout()
        {
            var frame = (RectTransform)panel.transform;
            lastCanvasSize = ((RectTransform)frame.parent).rect.size;
            bool wide = lastCanvasSize.x >= 1200;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1, 0);
            frame.anchoredPosition = new Vector2(-40, 36);
            float width = wide ? Mathf.Min(1400, lastCanvasSize.x - 520) : lastCanvasSize.x - 80;
            // 대사 칸은 창 양옆에서 64씩 들어가 있다.
            float linesHeight = linesText.GetPreferredValues(linesText.text, width - 128, 0).y;
            frame.sizeDelta = new Vector2(width, DialoguePanelLayout.Height(wide, linesHeight));
            helpPanel.anchorMin = helpPanel.anchorMax = helpPanel.pivot = new Vector2(0, .5f);
            helpPanel.anchoredPosition = new Vector2(40, wide ? -12 : 200);
            helpPanel.sizeDelta = new Vector2(
                wide ? 390 : Mathf.Min(390, lastCanvasSize.x - 80),
                Mathf.Max(220, Mathf.Min(680, lastCanvasSize.y - (wide ? 280 : 600)))
            );
            if (vehicleLayout && wide)
            {
                frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
                frame.anchoredPosition = new Vector2(40, 36);
                helpPanel.anchorMin = helpPanel.anchorMax = helpPanel.pivot = new Vector2(1, .5f);
                helpPanel.anchoredPosition = new Vector2(-40, -12);
            }
            ((RectTransform)npcNameText.transform).anchoredPosition = new Vector2(
                64,
                frame.sizeDelta.y - 96
            );
            // 이름 바로 오른쪽에 두고, 오른쪽 위 [다시 시도]·[대화 종료] 앞에서 말줄임으로 자른다.
            var nameRect = (RectTransform)npcNameText.transform;
            float standingX = 64 + Mathf.Min(npcNameText.preferredWidth, nameRect.sizeDelta.x) + 24;
            var standingRect = (RectTransform)standingText.transform;
            standingRect.anchoredPosition = new Vector2(standingX, frame.sizeDelta.y - 96);
            standingRect.sizeDelta = new Vector2(
                Mathf.Max(0, width - standingX - 500),
                nameRect.sizeDelta.y
            );
            // 대사 칸은 상태 줄 위에서 끝나 마지막 줄이 도구 버튼에 걸리지 않는다.
            var history = (RectTransform)historyScroll.transform;
            history.anchoredPosition = new Vector2(64, DialoguePanelLayout.LinesBottom);
            history.sizeDelta = new Vector2(
                -128,
                frame.sizeDelta.y - DialoguePanelLayout.ControlsHeight
            );
            var input = (RectTransform)inputField.transform;
            input.sizeDelta = new Vector2(Mathf.Max(160, width - 600), 72);
            // 상태 줄은 도구 줄 바로 위에서 창 안쪽 폭 전체를 쓴다. 음성 동의처럼 긴 안내는 글자를 줄여 한 줄에 맞춘다.
            var status = (RectTransform)statusText.transform;
            status.anchoredPosition = new Vector2(64, DialoguePanelLayout.StatusBottom);
            status.sizeDelta = new Vector2(width - 128, DialoguePanelLayout.StatusHeight);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14;
            statusText.fontSizeMax = 20;
            ((RectTransform)sendButton.transform).anchoredPosition = new Vector2(width - 520, 44);
            ((RectTransform)talkButton.transform).anchoredPosition = new Vector2(width - 328, 44);
        }

        private string FormatGuide()
        {
            if (content.IsGuidedCompleted)
            {
                return "길 안내를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요.";
            }

            if (content.IsGuided)
            {
                return $"이번에 읽을 대사: <b>{content.SuggestedEnglish}</b>\n{content.SuggestedKorean}";
            }

            if (!content.UsesVoice)
            {
                return "정답은 없어요. 하고 싶은 말을 영어로 자유롭게 입력해 보세요.";
            }

            return content.CanTalk
                ? FreeTalkGuide + " [꾹 눌러 말하기]로 말하면 문장으로 바꿔 드려요."
                : FreeTalkGuide + " " + VoiceInputNotice.ConsentRequired;
        }

        private bool NeedsVoiceConsent => content.UsesVoice && !content.CanTalk;
    }
}
