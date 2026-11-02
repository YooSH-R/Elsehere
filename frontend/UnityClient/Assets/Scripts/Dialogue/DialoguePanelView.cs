using System;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 화면. 이번 대화의 대사, 안내·상태 문구, 추천 단어, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[대화 종료],
    /// 서버 AI 대화의 [말하기]·[다시 듣기]·[힌트]·[다시 시도]가 있다. 음성 처리에 동의하지 않았으면 [말하기]를 회색으로 두고,
    /// 누르면 그 자리에서 동의할 수 있는 창을 연다. [뜻 보기]를 펼친 동안 대사의 풀이 단어를 누르면 그 단어 근처에 뜻과
    /// [단어장에 저장]이 있는 호버 카드를 띄운다.
    /// </summary>
    public class DialoguePanelView : MonoBehaviour
    {
        private const string FreeTalkGuide =
            "정답은 없어요. 영어로 자유롭게 말하거나 입력해 보세요. 한국어를 섞어도 돼요.";
        private const string TalkCaption = "말하기";
        private const string StopTalkCaption = "종료";

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

        [SerializeField]
        [Tooltip("NPC 첫 대사·대답을 기다리는 동안 대사 칸을 덮는 로딩")]
        private GameObject waitPanel;

        [SerializeField]
        private TMP_Text waitText;

        [SerializeField]
        [Tooltip("대화를 끝내고 피드백을 기다리는 동안 화면 전체를 덮는 로딩 창")]
        private GameObject feedbackWait;
        private Vector2 lastCanvasSize;

        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private Button exampleButton;

        [SerializeField]
        private Button talkButton;

        [SerializeField]
        private TMP_Text talkCaption;

        // 녹음 중에 켜는 불. 목소리가 들리면 초록색, 조용하면 흐린 흰색이다.
        [SerializeField]
        private Image voiceLight;

        [SerializeField]
        private Button meaningButton;

        [SerializeField]
        [Tooltip("대사 칸의 밑줄 친 풀이 단어 클릭")]
        private TextLinkClicks lineLinks;

        [SerializeField]
        [Tooltip("누른 풀이 단어 근처에 뜨는 호버 카드. 단어·뜻과 [단어장에 저장]이 있다.")]
        private RectTransform wordCard;

        [SerializeField]
        private TMP_Text wordTitleText;

        [SerializeField]
        private TMP_Text wordMeaningText;

        [SerializeField]
        [Tooltip("저장하는 중·저장 결과·실패 이유")]
        private TMP_Text wordNoteText;

        [SerializeField]
        private Button wordSaveButton;

        [SerializeField]
        private Button wordCloseButton;

        [SerializeField]
        private Button replayButton;

        [SerializeField]
        private Button hintButton;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        [Tooltip("음성 처리에 동의하지 않은 채 회색 [말하기]를 누르면 뜨는 동의 창")]
        private VoiceConsentDialogView voiceConsentDialog;

        private IDialogueContent content;
        private bool isMeaningVisible;
        private GlossaryWordCard glossaryCard = new GlossaryWordCard();
        private ConversationStandingDisplay standingDisplay = new ConversationStandingDisplay();
        private ThemedElement sendTheme;
        private CanvasGroup talkGroup;
        private bool isRecording;

        // 음성 처리에 동의하지 않아 쓸 수 없는 [말하기]의 투명도. 누를 수는 있어 동의 창을 연다.
        private const float UnavailableTalkAlpha = 0.45f;

        // 단어 카드와 대화창 가장자리 사이 여백
        private const float WordCardMargin = 12;

        /// <summary>녹음 중 목소리가 들릴 때 [종료] 버튼의 불 색.</summary>
        public static readonly Color VoiceHeardColor = new Color32(76, 217, 100, 255);
        private static readonly Color VoiceQuietColor = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>[대화 종료]를 누르면 알린다.</summary>
        public event Action EndRequested;

        /// <summary>[보내기]나 Enter로 입력한 문장을 알린다.</summary>
        public event Action<string> SendRequested;

        /// <summary>녹음 중이 아닐 때 [말하기]를 누르면 알린다.</summary>
        public event Action TalkStartRequested;

        /// <summary>녹음 중에 [종료]를 누르면 알린다.</summary>
        public event Action TalkStopRequested;

        /// <summary>[다시 듣기]를 누르면 알린다.</summary>
        public event Action ReplayRequested;

        /// <summary>[뜻 보기]로 뜻을 펼쳤는데 아직 뜻이 없으면 알린다. 서버 대화는 이때 뜻을 받아 온다.</summary>
        public event Action MeaningRequested;

        /// <summary>[힌트]를 누르면 알린다.</summary>
        public event Action HintRequested;

        /// <summary>대사에서 고른 풀이 단어의 [단어장에 저장]을 누르면 그 단어를 알린다. 결과는 <see cref="ShowWordSaveResult"/>로 받는다.</summary>
        public event Action<GlossaryWord> WordSaveRequested;

        /// <summary>[다시 시도]를 누르면 알린다.</summary>
        public event Action RetryRequested;

        public bool IsVisible => panel.activeSelf;

        /// <summary>음성 처리 동의 창. 대화 처리가 [동의하고 켜기]·[문서 보기]를 받아 처리한다.</summary>
        public VoiceConsentDialogView VoiceConsentDialog => voiceConsentDialog;

        /// <summary>풀이 단어의 [단어장에 저장]을 보여 줄지. 단어장 서버가 연결되지 않았으면 끈다.</summary>
        public bool IsWordSaveAvailable { get; set; } = true;

        /// <summary>풀이 단어를 저장한 결과를 단어 카드에 보인다.</summary>
        /// <param name="saved">단어장에 들어갔는지. 이미 있던 단어도 true다.</param>
        public void ShowWordSaveResult(GlossaryWord word, bool saved, string message)
        {
            glossaryCard.FinishSaving(word, saved, message);
            ShowWordCard();
        }

        public void Show(IDialogueContent shownContent)
        {
            content = shownContent;
            isMeaningVisible = false;
            // 저장한 단어 표시는 이번 대화 안에서만 기억한다.
            glossaryCard = new GlossaryWordCard { IsSaveAvailable = IsWordSaveAvailable };
            standingDisplay = new ConversationStandingDisplay();
            inputField.text = "";
            ShowVoiceInput(isRecording: false, isHearingVoice: false);
            panel.SetActive(true);
            helpPanel.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            content = null;
            panel.SetActive(false);
            helpPanel.gameObject.SetActive(false);
            voiceConsentDialog.Hide();
            ShowWait(DialogueWait.None);
        }

        // NPC 첫 대사·대답은 대사 칸을 덮는 로딩으로, 오래 걸리는 피드백은 화면 가운데 로딩 창으로 알린다.
        private void ShowWait(DialogueWait wait)
        {
            bool inLines = wait == DialogueWait.Opening || wait == DialogueWait.NpcReply;
            waitPanel.SetActive(inLines);
            // 로딩 카드는 대사 칸과 같은 크기라 둥근 모서리 틈으로 대사가 비치지 않게 그동안 대사를 감춘다.
            linesText.enabled = !inLines;
            if (inLines)
            {
                waitText.text =
                    wait == DialogueWait.Opening
                        ? "대화를 준비하고 있어요"
                        : $"{content.NpcName}의 대답을 기다리고 있어요";
            }

            feedbackWait.SetActive(wait == DialogueWait.Feedback);
        }

        /// <summary>
        /// 녹음 중인지와 지금 목소리가 들리는지를 [말하기] 버튼에 표시한다. 녹음 중이면 [종료]로 바꾸고 불을 켠다.
        /// 녹음은 시간·무음으로도 저절로 끝나므로 대화 처리가 매 프레임 부른다.
        /// </summary>
        public void ShowVoiceInput(bool isRecording, bool isHearingVoice)
        {
            this.isRecording = isRecording;
            talkCaption.text = isRecording ? StopTalkCaption : TalkCaption;
            voiceLight.gameObject.SetActive(isRecording);
            voiceLight.color = isHearingVoice ? VoiceHeardColor : VoiceQuietColor;
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

            // 밑줄은 뜻을 펼친 동안만 보이므로, 뜻을 접으면(보내기 포함) 단어 카드도 닫는다.
            if (!isMeaningVisible)
                glossaryCard.Close();
            ShowLines();
            npcNameText.text = content.NpcName;
            UpdateStanding();
            guideText.text = FormatGuide();
            guideText.gameObject.SetActive(content.IsGuided);
            statusText.text = FormatStatus();
            // 녹음 중·전사 뒤처럼 바쁘지 않아도 할 일을 알리는 안내가 있으면 보인다.
            // 음성 처리에 동의하지 않아 [말하기]가 회색이면, 눌러서 동의하면 말할 수 있다고 입력칸 위에 알린다.
            // NPC 대답·피드백을 기다리는 동안은 상태 줄 대신 로딩이 같은 내용을 크게 알린다.
            statusText.gameObject.SetActive(
                content.Waiting == DialogueWait.None
                    && (
                        content.IsBusy
                        || content.CanRetry
                        || content.IsGuidedCompleted
                        || NeedsVoiceConsent
                        || !string.IsNullOrEmpty(content.StatusMessage)
                    )
            );
            ShowWait(content.Waiting);
            suggestText.text = DialogueHelpText.Words(
                content.SuggestWords,
                content.IsSuggestingWords
            );
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
            ShowWordCard();
            TalkButtonState talk = TalkState();
            talkButton.gameObject.SetActive(talk.IsVisible);
            talkButton.interactable = talk.Action != TalkButtonAction.None;
            TalkGroup.alpha = talk.LooksAvailable ? 1f : UnavailableTalkAlpha;
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
            lineLinks.Clicked += SelectWord;
            wordSaveButton.onClick.AddListener(RequestWordSave);
            wordCloseButton.onClick.AddListener(CloseWordCard);
            // 대사 칸을 스크롤하면 카드가 고른 단어를 따라간다.
            historyScroll.onValueChanged.AddListener(_ => PlaceWordCard());
            replayButton.onClick.AddListener(() => ReplayRequested?.Invoke());
            hintButton.onClick.AddListener(() => HintRequested?.Invoke());
            retryButton.onClick.AddListener(RequestRetry);
            talkButton.onClick.AddListener(PressTalk);
            Hide();
        }

        // [말하기]를 흐리게 그리는 투명도 묶음. Awake 전에 그릴 수도 있어(에디터 테스트 등) 처음 쓸 때 붙인다.
        private CanvasGroup TalkGroup
        {
            get
            {
                if (talkGroup == null && !talkButton.TryGetComponent(out talkGroup))
                {
                    talkGroup = talkButton.gameObject.AddComponent<CanvasGroup>();
                }

                return talkGroup;
            }
        }

        private TalkButtonState TalkState() =>
            TalkButtonRules.Decide(
                content.UsesVoice,
                content.CanTalk,
                content.CanSend,
                isRecording
            );

        private void PressTalk()
        {
            if (content == null)
            {
                return;
            }

            switch (TalkState().Action)
            {
                case TalkButtonAction.StartRecording:
                    TalkStartRequested?.Invoke();
                    break;
                case TalkButtonAction.StopRecording:
                    TalkStopRequested?.Invoke();
                    break;
                case TalkButtonAction.AskVoiceConsent:
                    voiceConsentDialog.Show();
                    break;
            }
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

        // 지난 대사의 단어를 눌러도 보던 자리에 머물도록 대사 칸을 맨 아래로 내리지 않고 고른 단어 표시와 카드만 바꾼다.
        private void SelectWord(string linkId)
        {
            GlossaryWord word =
                content != null && isMeaningVisible
                    ? DialogueLinesText.FindWord(content.Lines, linkId)
                    : null;
            if (word == null)
                return;
            glossaryCard.Select(word);
            ShowLines();
            ShowWordCard();
        }

        private void CloseWordCard()
        {
            glossaryCard.Close();
            if (content == null)
                return;
            ShowLines();
            ShowWordCard();
        }

        private void RequestWordSave()
        {
            if (!glossaryCard.IsSaveInteractable)
                return;
            GlossaryWord word = glossaryCard.Word;
            glossaryCard.StartSaving();
            ShowWordCard();
            WordSaveRequested?.Invoke(word);
        }

        // 대사 칸 글자. 단어 카드에 보이는 단어는 대사 안에서도 칠한다.
        private void ShowLines()
        {
            linesText.text = DialogueLinesText.Format(
                content.Lines,
                content.NpcName,
                isMeaningVisible,
                content.LastNpcGlossary,
                glossaryCard.Word
            );
        }

        // 호버 카드는 대사 위에 겹쳐 뜨므로 대사 칸 크기는 그대로 두고 카드 내용과 자리만 맞춘다.
        private void ShowWordCard()
        {
            wordCard.gameObject.SetActive(glossaryCard.IsVisible);
            if (!glossaryCard.IsVisible)
                return;
            wordTitleText.richText = wordMeaningText.richText = false;
            wordTitleText.text = glossaryCard.Word.Text;
            wordMeaningText.text = glossaryCard.Word.MeaningKo;
            wordNoteText.text = glossaryCard.Note ?? "";
            wordNoteText.gameObject.SetActive(!string.IsNullOrEmpty(glossaryCard.Note));
            wordSaveButton.gameObject.SetActive(glossaryCard.IsSaveVisible);
            wordSaveButton.interactable = glossaryCard.IsSaveInteractable;
            // 저장 버튼이 없으면 안내 문구가 카드 아래쪽 폭을 다 쓴다.
            var note = (RectTransform)wordNoteText.transform;
            float saveWidth = ((RectTransform)wordSaveButton.transform).sizeDelta.x;
            note.offsetMax = new Vector2(
                glossaryCard.IsSaveVisible ? -(saveWidth + 28) : -16,
                note.offsetMax.y
            );
            PlaceWordCard();
        }

        // 누른 단어 바로 위(자리가 없으면 아래)에 카드를 띄운다. 대화창 안에서만 움직여 그 위의 NPC 얼굴을 가리지 않는다.
        private void PlaceWordCard()
        {
            if (content == null || !glossaryCard.IsVisible || !TryFindWordArea(out Rect word))
                return;
            var frame = (RectTransform)panel.transform;
            var area = new Rect(Vector2.zero, frame.rect.size);
            area.min += Vector2.one * WordCardMargin;
            area.max -= Vector2.one * WordCardMargin;
            wordCard.anchoredPosition = WordCardPlacement.Place(word, wordCard.rect.size, area);
        }

        // 고른 단어의 링크 글자들이 차지한 영역을 대화창 왼쪽 아래 기준 좌표로 구한다.
        private bool TryFindWordArea(out Rect area)
        {
            area = default;
            linesText.ForceMeshUpdate();
            TMP_TextInfo info = linesText.textInfo;
            for (int i = 0; i < info.linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                if (
                    DialogueLinesText.FindWord(content.Lines, link.GetLinkID()) != glossaryCard.Word
                )
                    continue;
                TMP_CharacterInfo first = info.characterInfo[link.linkTextfirstCharacterIndex];
                TMP_CharacterInfo last = info.characterInfo[
                    link.linkTextfirstCharacterIndex + link.linkTextLength - 1
                ];
                var frame = (RectTransform)panel.transform;
                Vector2 min = PanelPoint(frame, first.bottomLeft);
                Vector2 max = PanelPoint(frame, last.topRight);
                area = Rect.MinMaxRect(
                    Mathf.Min(min.x, max.x),
                    Mathf.Min(min.y, max.y),
                    Mathf.Max(min.x, max.x),
                    Mathf.Max(min.y, max.y)
                );
                return true;
            }

            return false;
        }

        private Vector2 PanelPoint(RectTransform frame, Vector3 linesPoint)
        {
            Vector3 world = linesText.transform.TransformPoint(linesPoint);
            return (Vector2)frame.InverseTransformPoint(world) - frame.rect.min;
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

        // 대화창 윗변과 택시 안내 사이 간격
        private const float VehicleStatusGap = 16;

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
            // 창 크기가 바뀌어 대사 칸 높이가 달라져도 보던 아래쪽(보통 가장 최근 대사와 단어 풀이)이 가려지지 않게 스크롤 위치를 지킨다.
            var history = (RectTransform)historyScroll.transform;
            float scroll = historyScroll.verticalNormalizedPosition;
            history.anchoredPosition = new Vector2(64, DialoguePanelLayout.LinesBottom);
            history.sizeDelta = new Vector2(
                -128,
                frame.sizeDelta.y - DialoguePanelLayout.ControlsHeight
            );
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = scroll;
            PlaceWordCard();
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
            PlaceVehicleStatus(frame);
        }

        // 택시 안내는 대화창 바로 위 가운데에 붙인다. 화면 위쪽 가운데에 두면 왼쪽 위 목표 카드와 겹쳤다.
        // 안내 판은 대화창과 같은 캔버스 아래에 있어 대화창의 자리를 그대로 기준으로 삼는다.
        private void PlaceVehicleStatus(RectTransform frame)
        {
            if (vehicleStatusText == null)
                return;
            var status = (RectTransform)vehicleStatusText.transform.parent;
            status.anchorMin = status.anchorMax = frame.anchorMin;
            status.pivot = new Vector2(.5f, 0);
            status.anchoredPosition =
                frame.anchoredPosition
                + new Vector2(
                    (.5f - frame.pivot.x) * frame.sizeDelta.x,
                    frame.sizeDelta.y + VehicleStatusGap
                );
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
                ? FreeTalkGuide + " [말하기]를 누르고 말한 뒤 다시 누르면 문장으로 바꿔 드려요."
                : FreeTalkGuide + " " + VoiceInputNotice.ConsentRequired;
        }

        private bool NeedsVoiceConsent => content.UsesVoice && !content.CanTalk;
    }
}
