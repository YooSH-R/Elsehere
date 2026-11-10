using System;
using System.Collections.Generic;
using System.Linq;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 화면. 이번 대화의 대사, 상태 문구, 추천 단어 카드, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[대화 종료],
    /// 서버 AI 대화와 튜토리얼 길 안내의 [말하기], 서버 AI 대화의 [다시 듣기]·[힌트]·[다시 시도]가 있다.
    /// 추천 문장은 별도 카드에서 [힌트]로 펼치고 가린다. 펼친 문장을 직접 누르면 영어만 입력창에 넣는다.
    /// 음성 처리에 동의하지 않았으면 [말하기]를 회색으로 두고, 누르면 그 자리에서 동의할 수 있는 창을 연다.
    /// [뜻 보기]를 펼친 동안 대사의 풀이 단어를 누르면 그 단어 근처에 뜻과 [단어장에 저장]이 있는 호버 카드를 띄운다.
    /// NPC 대사를 음성으로 들려주는 대화는 [자막]으로 대사를 단어마다 회색 막대로 가려 듣고 이해하게 한다. 처음에는 자막이 꺼져 있다.
    /// </summary>
    public class DialoguePanelView : MonoBehaviour
    {
        private const string TalkCaption = "말하기";
        private const string StopTalkCaption = "종료";

        // 녹음을 시작했지만 마이크가 아직 열리는 중. 이때 한 말은 녹음되지 않는다. 눌러서 그만둘 수는 있다.
        private const string PreparingTalkCaption = "준비 중";

        private const string EnableSubtitlesCaption = "자막 켜기";
        private const string DisableSubtitlesCaption = "자막 끄기";

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
        private TMP_Text statusText;

        [SerializeField]
        private RectTransform translationCard;

        [SerializeField]
        private TMP_Text translationText;

        [SerializeField]
        private Button translationListenButton;

        [SerializeField]
        private RectTransform translationDivider;

        [SerializeField]
        private TMP_Text suggestText;

        [SerializeField]
        private RectTransform helpPanel;

        [SerializeField]
        private DialogueSuggestionsView suggestions;

        private bool hintVisible;
        private DialogueLine hintLine;

        [SerializeField]
        private Button reviewFoldButton;

        [SerializeField]
        private ReviewReferenceBadges reviewBadges;
        private string reviewReference;
        private string reviewScenario;
        private string originalHelpTitle;
        private TMP_Text reviewHelpTitle;
        private bool reviewFolded;
        private float originalSuggestFont;
        private float originalSuggestHeight;
        private RectOffset originalSuggestPadding;
        private Scrollbar reviewScrollbar;

        public void SetReviewReference(string text)
        {
            if (reviewBadges != null)
                reviewBadges.Clear();
            if (text == null)
                reviewScenario = null;
            if (text == null || reviewReference == null)
                reviewFolded = false;
            var layout = suggestText.transform.parent.GetComponent<VerticalLayoutGroup>();
            var size = suggestText.GetComponent<LayoutElement>();
            if (text != null && reviewReference == null)
            {
                originalSuggestFont = suggestText.fontSize;
                originalSuggestHeight = size != null ? size.preferredHeight : -1;
                if (layout != null)
                    originalSuggestPadding = new RectOffset(
                        layout.padding.left,
                        layout.padding.right,
                        layout.padding.top,
                        layout.padding.bottom
                    );
            }
            if (text == null && reviewReference != null)
            {
                suggestText.fontSize = originalSuggestFont;
                if (size != null)
                    size.preferredHeight = originalSuggestHeight;
                if (layout != null && originalSuggestPadding != null)
                    layout.padding = originalSuggestPadding;
                suggestText.gameObject.SetActive(true);
            }
            reviewReference = text;
            // 고정된 바깥 카드는 유지하고, 그 안의 글자와 사용 표시만 스크롤한다.
            var reviewCard = suggestText.transform.parent;
            var innerTheme = reviewCard.GetComponent<ThemedElement>();
            innerTheme.enabled = text == null;
            reviewCard.GetComponent<Image>().enabled = text == null;
            var outerTheme = helpPanel.GetComponent<ThemedElement>();
            if (outerTheme != null)
                outerTheme.enabled = text != null;
            helpPanel.GetComponent<Image>().enabled = text != null;
            var reviewViewport = (RectTransform)helpPanel.Find("Viewport");
            var reviewScroll = reviewViewport.GetComponent<ScrollRect>();
            reviewViewport.offsetMin = text != null ? new Vector2(24, 20) : Vector2.zero;
            reviewViewport.offsetMax = text != null ? new Vector2(-24, -20) : Vector2.zero;
            if (text != null)
            {
                var outer =
                    helpPanel.GetComponent<ThemedElement>()
                    ?? helpPanel.gameObject.AddComponent<ThemedElement>();
                outer.enabled = true;
                outer.Use(innerTheme.Catalog, UiThemeRole.Card);
                if (reviewScrollbar == null && historyScroll.verticalScrollbar != null)
                {
                    reviewScrollbar = Instantiate(historyScroll.verticalScrollbar, helpPanel);
                    reviewScrollbar.name = "ReviewScrollbar";
                    var bar = (RectTransform)reviewScrollbar.transform;
                    bar.anchorMin = new Vector2(1, 0);
                    bar.anchorMax = Vector2.one;
                    bar.pivot = new Vector2(1, .5f);
                    bar.offsetMin = new Vector2(-12, 20);
                    bar.offsetMax = new Vector2(-6, -20);
                    reviewScrollbar.handleRect.GetComponent<Image>().color = new Color(
                        0.3f,
                        0.25f,
                        0.2f,
                        .35f
                    );
                }
                reviewScroll.verticalScrollbar = reviewScrollbar;
                reviewScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            else if (reviewScrollbar != null)
            {
                reviewScroll.verticalScrollbar = null;
                reviewScrollbar.gameObject.SetActive(false);
            }
            if (reviewHelpTitle == null)
            {
                reviewHelpTitle = suggestText
                    .transform.parent.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(item => item != suggestText);
                if (reviewHelpTitle != null)
                    originalHelpTitle = reviewHelpTitle.text;
            }
            Refresh();
        }

        public void SetReviewReference(
            IReadOnlyList<ReviewReferenceItem> items,
            int verifiedCount,
            string scenario = null
        )
        {
            reviewScenario = scenario;
            ReviewReferenceText formatted = ReviewReferenceText.Format(
                items,
                verifiedCount,
                scenario
            );
            SetReviewReference(formatted.Text);
            if (reviewBadges != null)
                reviewBadges.Show(formatted);
        }

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

        [SerializeField]
        private Button feedbackLaterButton;
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
        [Tooltip(
            "누른 풀이 단어 근처에 뜨는 호버 카드. 그 자리를 덮는 풀이(단어와 그 단어가 든 숙어)마다 뜻과 [단어장에 저장]이 있다."
        )]
        private RectTransform wordCard;

        [SerializeField]
        [Tooltip(
            "카드의 풀이 목록. 풀이가 여럿이면 다음 풀이 제목이 살짝 보이는 높이에서 스크롤한다."
        )]
        private ScrollRect wordEntries;

        [SerializeField]
        [Tooltip(
            "풀이 한 줄(Title·Meaning·Note·SaveButton). 풀이가 여럿이면 복제해 아래로 쌓는다."
        )]
        private RectTransform wordEntryTemplate;

        [SerializeField]
        private Button wordCloseButton;

        [SerializeField]
        private Button replayButton;

        [SerializeField]
        private Button hintButton;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        [Tooltip("NPC 대사 자막을 켜고 끄는 버튼. 켜 두면 강조색으로 칠한다.")]
        private Button subtitleButton;

        [SerializeField]
        [Tooltip("음성 처리에 동의하지 않은 채 회색 [말하기]를 누르면 뜨는 동의 창")]
        private VoiceConsentDialogView voiceConsentDialog;

        private IDialogueContent content;
        private bool isMeaningVisible;
        private GlossaryWordCard glossaryCard = new GlossaryWordCard();
        private readonly List<WordEntryRow> wordRows = new List<WordEntryRow>();
        private ConversationStandingDisplay standingDisplay = new ConversationStandingDisplay();
        private ThemedElement sendTheme;
        private CanvasGroup talkGroup;
        private bool isRecording;
        private NpcLineCovers npcLineCovers;

        // 입력창에 넣은 추천 문장. 같은 문장을 다시 그릴 때마다 넣으면 사용자가 고친 문장을 덮는다.
        private bool awaitingHint;

        // 음성 처리에 동의하지 않아 쓸 수 없는 [말하기]의 투명도. 누를 수는 있어 동의 창을 연다.
        private const float UnavailableTalkAlpha = 0.45f;

        // 단어 카드와 대화창 가장자리 사이 여백
        private const float WordCardMargin = 12;

        // 풀이가 여럿이면 카드 아래쪽에 다음 풀이의 제목이 보이도록 한 줄보다 이만큼 더 높게 두고 나머지는 스크롤한다.
        private const float NextWordPeek = 64;

        // 대사 칸에 한 번에 보이는 최근 대사 수(바로 앞 대사와 그다음 대사)
        private const int VisibleLineCount = 4;

        /// <summary>녹음 중 목소리가 들릴 때 [종료] 버튼의 불 색.</summary>
        public static readonly Color VoiceHeardColor = new Color32(76, 217, 100, 255);
        private static readonly Color VoiceQuietColor = new Color(1f, 1f, 1f, 0.55f);

        // 자유 대화의 입력칸 안내("영어로 답하기…"). 정해진 대사를 따라가는 대화에서는 그 대사로 바꿔 보인다.
        private string freePlaceholder;

        /// <summary>대사와 화자 이름을 함께 밝히는 튜토리얼 강조 영역.</summary>
        public IReadOnlyList<RectTransform> DialogueLinesParts =>
            new[] { LinesRect, npcNameText.rectTransform };

        /// <summary>
        /// 정해진 대사를 말하거나 입력하는 입력칸·[보내기]·[예시 문장]·[말하기]. 튜토리얼 코치 마크가 한 구멍으로 밝힌다.
        /// </summary>
        public IReadOnlyList<RectTransform> GuidedLineParts
        {
            get
            {
                var parts = new List<RectTransform>
                {
                    (RectTransform)inputField.transform,
                    (RectTransform)sendButton.transform,
                    (RectTransform)hintButton.transform,
                };
                if (talkButton.gameObject.activeSelf)
                {
                    parts.Add((RectTransform)talkButton.transform);
                }

                return parts;
            }
        }

        /// <summary>주고받은 대사 칸. 튜토리얼 코치 마크가 대화 시작 티칭에서 밝힌다.</summary>
        public RectTransform LinesRect => (RectTransform)historyScroll.transform;

        /// <summary>[뜻]·[힌트]·확인용 [예시 문장] 중 보이는 버튼.</summary>
        public IReadOnlyList<RectTransform> ToolParts
        {
            get
            {
                var parts = new List<RectTransform>();
                foreach (Button button in new[] { meaningButton, hintButton, exampleButton })
                {
                    if (button.gameObject.activeSelf)
                    {
                        parts.Add((RectTransform)button.transform);
                    }
                }

                return parts;
            }
        }

        /// <summary>왼쪽 도움 칸의 추천 단어 카드. 튜토리얼 코치 마크가 밝힌다.</summary>
        public RectTransform SuggestedWordsRect =>
            suggestions != null && reviewReference == null
                ? suggestions.WordsRect
                : (RectTransform)suggestText.transform.parent;

        /// <summary>NPC 이름과 표시 중인 호감도·무례 경고를 함께 밝힌다.</summary>
        public IReadOnlyList<RectTransform> RelationshipParts =>
            standingText.gameObject.activeInHierarchy
                ? new[] { npcNameText.rectTransform, standingText.rectTransform }
                : new[] { npcNameText.rectTransform };

        /// <summary>[대화 종료]. 튜토리얼 코치 마크가 길 안내를 마친 뒤 밝힌다.</summary>
        public RectTransform EndButtonRect => (RectTransform)endButton.transform;

        /// <summary>[대화 종료]를 누르면 알린다.</summary>
        public event Action EndRequested;

        public event Action FeedbackLaterRequested;

        /// <summary>[보내기]나 Enter로 입력한 문장을 알린다.</summary>
        public event Action<string> SendRequested;

        /// <summary>녹음 중이 아닐 때 [말하기]를 누르면 알린다.</summary>
        public event Action TalkStartRequested;

        /// <summary>녹음 중에 [종료]를 누르면 알린다.</summary>
        public event Action TalkStopRequested;

        /// <summary>[다시 듣기]를 누르면 알린다.</summary>
        public event Action ReplayRequested;
        public event Action<string> UserTranslationListenRequested;

        /// <summary>[뜻 보기]로 뜻을 펼쳤는데 아직 뜻이 없으면 알린다. 서버 대화는 이때 뜻을 받아 온다.</summary>
        public event Action MeaningRequested;

        /// <summary>추천 문장을 아직 받지 않았을 때 [힌트]를 누르면 알린다. 받은 뒤에는 묻지 않고 그 문장을 입력창에 다시 넣는다.</summary>
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
            awaitingHint = false;
            hintVisible = false;
            hintLine = shownContent.LastNpcLine;
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
            if (suggestions != null)
                suggestions.gameObject.SetActive(false);
            voiceConsentDialog.Hide();
            ShowWait(DialogueWait.None);
        }

        // NPC 첫 대사·대답은 대사 칸을 덮는 로딩으로, 오래 걸리는 피드백은 화면 가운데 로딩 창으로 알린다.
        private void ShowWait(DialogueWait wait)
        {
            bool inLines =
                wait == DialogueWait.Opening
                || wait == DialogueWait.NpcReply
                || wait == DialogueWait.NpcSpeech;
            waitPanel.SetActive(inLines);
            // 로딩 카드는 대사 칸과 같은 크기라 둥근 모서리 틈으로 대사가 비치지 않게 그동안 대사를 감춘다.
            linesText.enabled = !inLines;
            CoverHiddenWords();
            if (inLines)
            {
                waitText.text =
                    wait == DialogueWait.Opening ? "대화를 준비하고 있어요"
                    : wait == DialogueWait.NpcSpeech ? "음성을 준비하고 있어요"
                    : $"{content.NpcName}의 대답을 기다리고 있어요";
            }

            feedbackWait.SetActive(wait == DialogueWait.Feedback);
            // 복습 대화는 피드백 없이 연습 결과만 받으므로 나중에 확인할 것이 없다.
            if (feedbackLaterButton != null)
                feedbackLaterButton.gameObject.SetActive(
                    !(content is ServerConversation { IsReview: true })
                );
        }

        /// <summary>녹음 중 [말하기]에 켜는 불. 튜토리얼 코치 마크가 옆에 불의 뜻을 알린다.</summary>
        public RectTransform VoiceLightRect => voiceLight.rectTransform;

        /// <summary>녹음 중이라 [말하기]의 불이 켜져 있는지. 마이크가 열리는 중([준비 중])이면 꺼져 있다.</summary>
        public bool IsVoiceLightOn => voiceLight.gameObject.activeInHierarchy;

        /// <summary>
        /// 녹음 중인지와 지금 목소리가 들리는지를 [말하기] 버튼에 표시한다. 녹음 중이면 [종료]로 바꾸고 불을 켠다.
        /// 녹음을 시작했지만 마이크가 아직 열리는 중이면 [준비 중]을 보이고 불을 끈다.
        /// 녹음은 시간·무음으로도 저절로 끝나므로 대화 처리가 매 프레임 부른다.
        /// </summary>
        public void ShowVoiceInput(bool isRecording, bool isHearingVoice, bool isPreparing = false)
        {
            this.isRecording = isRecording;
            talkCaption.text =
                !isRecording ? TalkCaption
                : isPreparing ? PreparingTalkCaption
                : StopTalkCaption;
            voiceLight.gameObject.SetActive(isRecording && !isPreparing);
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

            // 새 NPC 대사에서는 다시 가리고, 늦게 도착한 힌트가 작성 중인 말을 바꾸지 않게 한다.
            if (!ReferenceEquals(hintLine, content.LastNpcLine))
            {
                hintLine = content.LastNpcLine;
                hintVisible = false;
                awaitingHint = false;
            }
            if (!string.IsNullOrEmpty(content.HintText))
                awaitingHint = false;

            // 밑줄은 뜻을 펼친 동안만 보이므로, 뜻을 접으면(보내기 포함) 단어 카드도 닫는다.
            if (!isMeaningVisible)
                glossaryCard.Close();
            ShowLines();
            ShowTranslation();
            npcNameText.text = content.NpcName;
            UpdateStanding();
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
                        || !string.IsNullOrEmpty(statusText.text)
                    )
            );
            ShowWait(content.Waiting);
            suggestText.text = DialogueHelpText.Words(
                content.SuggestWords,
                content.IsSuggestingWords
            );
            bool reviewing = reviewReference != null;
            if (suggestions != null)
            {
                suggestions.gameObject.SetActive(!reviewing || content.HasHint);
                suggestions.SetReferenceMode(reviewing);
                helpPanel.gameObject.SetActive(reviewing);
                suggestions.Show(
                    suggestText.text,
                    content.HasHint,
                    hintVisible,
                    content.HintText,
                    awaitingHint && string.IsNullOrEmpty(content.HintText),
                    content is ServerConversation hintServer
                        ? hintServer.HintMeaningKo
                        : content.SuggestedKorean
                );
            }
            if (reviewing)
                suggestText.text = reviewReference;
            suggestText.gameObject.SetActive(!reviewing || !reviewFolded);
            if (reviewHelpTitle != null)
                reviewHelpTitle.text = reviewing
                    ? (reviewScenario == null ? "이번에 써 볼 표현" : "이번 복습")
                    : originalHelpTitle;
            if (reviewFoldButton != null)
                reviewFoldButton.gameObject.SetActive(reviewing);

            sendButton.interactable = content.CanSend;
            // 정해진 대사를 따라가는 대화(튜토리얼 길 안내)는 대사를 다 마칠 때까지 끝낼 수 없다.
            endButton.interactable = !content.IsGuided || content.IsGuidedCompleted;
            // 시작에 실패했거나 끝난 대화에 입력할 수 있으면 [보내기]가 아무 일도 하지 않는 것처럼 보이므로 함께 막는다.
            inputField.interactable = content.CanSend;
            // 정해진 대사를 따라가는 대화는 이번에 말하거나 입력할 대사를 빈 입력칸에 회색 안내로 미리 보인다.
            var placeholder = (TMP_Text)inputField.placeholder;
            freePlaceholder ??= placeholder.text;
            placeholder.text = string.IsNullOrEmpty(content.SuggestedEnglish)
                ? freePlaceholder
                : content.SuggestedEnglish;
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
            hintButton.interactable = content.LastNpcLine != null && content.CanSend;
            retryButton.gameObject.SetActive(content.CanRetry);
            ShowSubtitleButton();
            UpdateLayout();
            Canvas.ForceUpdateCanvases();
            // 대사가 쌓이면 가장 최근 대사와 뜻이 보이게 맨 아래로 내린다. 지난 대사는 위로 스크롤해 다시 본다.
            historyScroll.verticalNormalizedPosition = 0;
        }

        private string pronunciationNotice;
        private string shownTranslation;

        public void ShowPronunciationFailure()
        {
            pronunciationNotice =
                "영어 음성을 재생할 수 없어요. 기기의 영어 음성과 소리 설정을 확인해 주세요.";
            Refresh();
        }

        private void ShowTranslation()
        {
            if (translationCard == null)
                return;
            string expression = content?.UserTranslation;
            if (shownTranslation != expression)
                pronunciationNotice = null;
            shownTranslation = expression;
            bool shown = !string.IsNullOrWhiteSpace(expression);
            translationCard.gameObject.SetActive(shown);
            translationText.text = shown ? expression : string.Empty;
            translationListenButton.interactable = shown && !content.IsBusy;
        }

        private void Awake()
        {
            if (reviewFoldButton != null)
                reviewFoldButton.onClick.AddListener(() =>
                {
                    reviewFolded = !reviewFolded;
                    reviewFoldButton.transform.Find("Arrow").localRotation = Quaternion.Euler(
                        0,
                        0,
                        reviewFolded ? -90 : 90
                    );
                    Refresh();
                });
            endButton.onClick.AddListener(() => EndRequested?.Invoke());
            if (feedbackLaterButton != null)
                feedbackLaterButton.onClick.AddListener(() => FeedbackLaterRequested?.Invoke());
            sendButton.onClick.AddListener(RequestSend);
            sendButton.TryGetComponent(out sendTheme);
            inputField.onSubmit.AddListener(_ => RequestSend());
            inputField.onValueChanged.AddListener(_ => HighlightSend());
            exampleButton.onClick.AddListener(FillExampleSentence);
            meaningButton.onClick.AddListener(ToggleMeaning);
            lineLinks.Clicked += SelectWord;
            wordRows.Add(CreateWordRow(0));
            wordCloseButton.onClick.AddListener(CloseWordCard);
            // 대사 칸을 스크롤하면 카드가 고른 단어를 따라간다.
            historyScroll.onValueChanged.AddListener(_ => PlaceWordCard());
            replayButton.onClick.AddListener(() => ReplayRequested?.Invoke());
            if (translationListenButton != null)
                translationListenButton.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrWhiteSpace(content?.UserTranslation) && !content.IsBusy)
                        UserTranslationListenRequested?.Invoke(content.UserTranslation);
                });
            hintButton.onClick.AddListener(PressHint);
            if (suggestions != null)
            {
                suggestions.HideRequested += PressHint;
                suggestions.SentenceSelected += () =>
                {
                    if (hintVisible && !string.IsNullOrEmpty(content?.HintText))
                        inputField.text = content.HintText;
                };
            }
            retryButton.onClick.AddListener(RequestRetry);
            subtitleButton.onClick.AddListener(ToggleSubtitles);
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

        // 펼치기와 입력을 분리한다. 이미 받은 문장을 다시 펼칠 때는 서버를 호출하지 않는다.
        private void PressHint()
        {
            if (content == null)
            {
                return;
            }

            hintVisible = !hintVisible;
            if (hintVisible && string.IsNullOrEmpty(content.HintText) && !awaitingHint)
            {
                awaitingHint = true;
                HintRequested?.Invoke();
            }
            Refresh();
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
            string countdown = content is ServerConversation server
                ? server.CountdownMessage
                : null;
            string message;
            if (string.IsNullOrEmpty(content.RecoveryGuide))
            {
                // 녹음·오류 같은 지금 상황 안내가 먼저이고, 없을 때만 음성 동의 안내를 보인다.
                message =
                    string.IsNullOrEmpty(content.StatusMessage) && NeedsVoiceConsent
                        ? VoiceInputNotice.ConsentRequired
                        : content.StatusMessage ?? "";
            }
            else
            {
                message = string.IsNullOrEmpty(content.StatusMessage)
                    ? content.RecoveryGuide
                    : $"{content.StatusMessage}\n{content.RecoveryGuide}";
            }

            if (pronunciationNotice != null)
                message = pronunciationNotice;

            return string.IsNullOrEmpty(countdown) ? message
                : string.IsNullOrEmpty(message) ? countdown
                : $"{countdown}\n{message}";
        }

        private void RequestSend()
        {
            if (content == null || !content.CanSend)
            {
                return;
            }

            string text = inputField.text;
            awaitingHint = false;
            inputField.text = "";
            isMeaningVisible = false;
            SendRequested?.Invoke(text);
        }

        private void FillExampleSentence()
        {
            inputField.text = content.ExampleSentence;
        }

        // 지난 대사의 단어를 눌러도 보던 자리에 머물도록 대사 칸을 맨 아래로 내리지 않고 고른 단어 표시와 카드만 바꾼다.
        // 숙어 안의 단어를 누르면 그 단어와 숙어를 함께 카드에 쌓고, 새로 고른 카드는 맨 위부터 보인다.
        private void SelectWord(string linkId)
        {
            if (content == null || !isMeaningVisible)
                return;
            IReadOnlyList<GlossaryWord> words = DialogueLinesText.FindWords(content.Lines, linkId);
            if (words.Count == 0)
                return;
            glossaryCard.Select(words);
            ShowLines();
            ShowWordCard();
            wordEntries.StopMovement();
            wordEntries.content.anchoredPosition = Vector2.zero;
        }

        private void CloseWordCard()
        {
            glossaryCard.Close();
            if (content == null)
                return;
            ShowLines();
            ShowWordCard();
        }

        // 카드의 index번째 풀이를 저장한다.
        private void RequestWordSave(int index)
        {
            if (index >= glossaryCard.Words.Count)
                return;
            GlossaryWord word = glossaryCard.Words[index];
            if (!glossaryCard.IsSaveInteractable(word))
                return;
            glossaryCard.StartSaving(word);
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
                glossaryCard.Word,
                SentenceIndent(),
                HidesNpcLines
            );
            CoverHiddenWords();
        }

        // 자막은 NPC 대사를 음성으로 들려주는 대화에서만 끌 수 있다. 음성이 없는 대본 대화나 음성 생성 실패·만료처럼
        // 지금 대사를 들을 수 없으면 가렸을 때 대사를 알 방법이 없으므로 보인다(기능명세 F-25).
        private bool HidesNpcLines =>
            content.SpeaksNpcLines
            && content.IsNpcSpeechReady
            && !NpcSubtitlePreference.ShowsSubtitles;

        private void ShowSubtitleButton()
        {
            subtitleButton.gameObject.SetActive(content.SpeaksNpcLines);
            bool showsSubtitles = NpcSubtitlePreference.ShowsSubtitles;
            subtitleButton.GetComponentInChildren<TMP_Text>(true).text = showsSubtitles
                ? DisableSubtitlesCaption
                : EnableSubtitlesCaption;
            if (subtitleButton.TryGetComponent(out ThemedElement theme))
                theme.SetSelected(showsSubtitles);
        }

        private void ToggleSubtitles()
        {
            NpcSubtitlePreference.ShowsSubtitles = !NpcSubtitlePreference.ShowsSubtitles;
            Refresh();
        }

        // 가린 대사의 단어 자리는 대사 칸 폭에 따라 줄바꿈이 달라지므로, 대사나 배치가 바뀔 때마다 막대를 다시 놓는다.
        private void CoverHiddenWords()
        {
            npcLineCovers ??= new NpcLineCovers(linesText);
            npcLineCovers.Redraw();
        }

        // 이름 길이와 상관없이 문장이 같은 자리에서 시작하도록, 더 긴 이름(NPC 이름·나) 뒤에 두 칸만큼 띄운 자리
        private float SentenceIndent()
        {
            float npc = linesText.GetPreferredValues($"<b>{content.NpcName}</b>").x;
            float me = linesText.GetPreferredValues("<b>나</b>").x;
            return Mathf.Ceil(Mathf.Max(npc, me) + linesText.fontSize * .6f);
        }

        // 호버 카드는 대사 위에 겹쳐 뜨므로 대사 칸 크기는 그대로 두고 카드 내용과 자리만 맞춘다.
        // 풀이마다 한 줄을 아래로 쌓고, 둘 이상이면 다음 풀이 제목이 보이는 높이에서 스크롤한다.
        private void ShowWordCard()
        {
            wordCard.gameObject.SetActive(glossaryCard.IsVisible);
            if (!glossaryCard.IsVisible)
                return;
            IReadOnlyList<GlossaryWord> words = glossaryCard.Words;
            while (wordRows.Count < words.Count)
                wordRows.Add(CreateWordRow(wordRows.Count));
            for (int i = 0; i < wordRows.Count; i++)
            {
                wordRows[i].Root.gameObject.SetActive(i < words.Count);
                if (i >= words.Count)
                    continue;
                wordRows[i].Root.anchoredPosition = new Vector2(0, -i * WordRowHeight);
                ShowWordRow(wordRows[i], words[i]);
            }

            SizeWordCard(WantedWordCardHeight());
            PlaceWordCard();
        }

        // 풀이 한 줄 높이. 풀이가 하나뿐일 때의 카드 높이와 같다.
        private float WordRowHeight => wordEntryTemplate.rect.height;

        // 풀이가 여럿이면 다음 풀이 제목이 보이도록 한 줄보다 높게 둔다.
        private float WantedWordCardHeight() =>
            glossaryCard.Words.Count > 1 ? WordRowHeight + NextWordPeek : WordRowHeight;

        // 카드 높이를 정하고, 목록 끝까지 내리면 마지막 풀이가 카드 맨 위에 오도록 목록 아래에 카드 높이만큼 자리를 둔다.
        private void SizeWordCard(float height)
        {
            wordCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            wordEntries.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                (glossaryCard.Words.Count - 1) * WordRowHeight + height
            );
        }

        private void ShowWordRow(WordEntryRow row, GlossaryWord word)
        {
            row.Title.richText = row.Meaning.richText = false;
            row.Title.text = word.Text;
            row.Meaning.text = word.MeaningKo;
            string note = glossaryCard.NoteFor(word);
            row.Note.text = note ?? "";
            row.Note.gameObject.SetActive(!string.IsNullOrEmpty(note));
            bool canSave = glossaryCard.IsSaveVisible(word);
            row.Save.gameObject.SetActive(canSave);
            row.Save.interactable = glossaryCard.IsSaveInteractable(word);
            // 저장 버튼이 없으면 안내 문구가 줄 아래쪽 폭을 다 쓴다.
            var noteRect = (RectTransform)row.Note.transform;
            float saveWidth = ((RectTransform)row.Save.transform).sizeDelta.x;
            noteRect.offsetMax = new Vector2(
                canSave ? -(saveWidth + 28) : -16,
                noteRect.offsetMax.y
            );
        }

        // 첫 줄은 프리팹의 풀이 줄을 그대로 쓰고, 그 뒤 줄은 복제한다. 줄마다 [단어장에 저장]이 자기 순서의 풀이를 저장한다.
        private WordEntryRow CreateWordRow(int index)
        {
            RectTransform root =
                index == 0
                    ? wordEntryTemplate
                    : Instantiate(wordEntryTemplate, wordEntryTemplate.parent);
            var row = new WordEntryRow(root);
            row.Save.onClick.AddListener(() => RequestWordSave(index));
            return row;
        }

        // 카드의 풀이 한 줄
        private sealed class WordEntryRow
        {
            public WordEntryRow(RectTransform root)
            {
                Root = root;
                Title = root.Find("Title").GetComponent<TMP_Text>();
                Meaning = root.Find("Meaning").GetComponent<TMP_Text>();
                Note = root.Find("Note").GetComponent<TMP_Text>();
                Save = root.Find("SaveButton").GetComponent<Button>();
            }

            public RectTransform Root { get; }
            public TMP_Text Title { get; }
            public TMP_Text Meaning { get; }

            // 저장하는 중·저장 결과·실패 이유
            public TMP_Text Note { get; }
            public Button Save { get; }
        }

        // 누른 단어 바로 위(자리가 없으면 아래)에 카드를 띄운다. 이름·호감도 줄 아래에 두어 표시가 카드 안으로 비치지 않게 한다.
        private void PlaceWordCard()
        {
            if (content == null || !glossaryCard.IsVisible || !TryFindWordArea(out Rect word))
                return;
            var frame = (RectTransform)panel.transform;
            var area = new Rect(Vector2.zero, frame.rect.size);
            area.min += Vector2.one * WordCardMargin;
            area.max -= Vector2.one * WordCardMargin;
            area.yMax = Mathf.Min(
                area.yMax,
                npcNameText.rectTransform.anchoredPosition.y - WordCardMargin
            );
            if (!string.IsNullOrWhiteSpace(content.UserTranslation))
            {
                var history = (RectTransform)historyScroll.transform;
                area.xMax = Mathf.Min(
                    area.xMax,
                    history.anchoredPosition.x + history.rect.width - WordCardMargin
                );
                area.yMax = Mathf.Min(area.yMax, history.anchoredPosition.y + history.rect.height);
            }
            // 높인 카드가 단어 위나 아래 자리에 다 들어가지 않으면 그 자리에 맞춰 줄여, 누른 단어를 가리지 않게 한다.
            SizeWordCard(
                WordCardPlacement.FitHeight(word, WantedWordCardHeight(), WordRowHeight, area)
            );
            wordCard.anchoredPosition = WordCardPlacement.Place(word, wordCard.rect.size, area);
        }

        // 카드가 먼저 보이는 풀이를 덮는 링크 조각들이 차지한 영역을 대화창 왼쪽 아래 기준 좌표로 구한다.
        // 숙어는 여러 조각으로 나뉘므로 조각을 모두 합친다.
        private bool TryFindWordArea(out Rect area)
        {
            area = default;
            bool found = false;
            linesText.ForceMeshUpdate();
            TMP_TextInfo info = linesText.textInfo;
            for (int i = 0; i < info.linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                if (
                    !DialogueLinesText
                        .FindWords(content.Lines, link.GetLinkID())
                        .Contains(glossaryCard.Word)
                )
                    continue;
                TMP_CharacterInfo first = info.characterInfo[link.linkTextfirstCharacterIndex];
                TMP_CharacterInfo last = info.characterInfo[
                    link.linkTextfirstCharacterIndex + link.linkTextLength - 1
                ];
                var frame = (RectTransform)panel.transform;
                Vector2 min = PanelPoint(frame, first.bottomLeft);
                Vector2 max = PanelPoint(frame, last.topRight);
                var piece = Rect.MinMaxRect(
                    Mathf.Min(min.x, max.x),
                    Mathf.Min(min.y, max.y),
                    Mathf.Max(min.x, max.x),
                    Mathf.Max(min.y, max.y)
                );
                area = found
                    ? Rect.MinMaxRect(
                        Mathf.Min(area.xMin, piece.xMin),
                        Mathf.Min(area.yMin, piece.yMin),
                        Mathf.Max(area.xMax, piece.xMax),
                        Mathf.Max(area.yMax, piece.yMax)
                    )
                    : piece;
                found = true;
            }

            return found;
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

        // 대화창 이름 줄 위와 택시 안내 사이 간격
        private const float VehicleStatusGap = 16;

        // 이름표 안 좌우 여백과 가장 좁은 폭
        private const float NamePadding = 22;
        private const float MinNameWidth = 120;

        // 오른쪽 위 도움 카드 자리. 위쪽은 메뉴 버튼 아래부터 쓴다.
        private const float HelpTop = 128;
        private const float HelpMargin = 40;
        private const float HelpWidth = 390;

        [SerializeField]
        private TMP_Text vehicleStatusText;

        [SerializeField]
        [Tooltip("NPC 이름 뒤의 코랄 이름표. 이름 길이에 맞춰 폭을 정한다.")]
        private RectTransform nameChip;

        /// <summary>택시에 탄 동안의 대화인지. 택시 안내는 택시에서만 보인다.</summary>
        public void SetVehicleLayout(bool enabled)
        {
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

        /// <summary>
        /// 대화창을 화면 아래 전체 폭에 깔고, 가운데 열에 입력 줄·상태 줄·대사·이름 줄을 쌓는다.
        /// 단어·문장 도움 카드는 오른쪽 위에 둔다.
        /// </summary>
        public void UpdateLayout()
        {
            var frame = (RectTransform)panel.transform;
            lastCanvasSize = ((RectTransform)frame.parent).rect.size;
            float column = Mathf.Min(
                DialogueCinemaLayout.ColumnWidth,
                lastCanvasSize.x - 2 * DialogueCinemaLayout.SideMargin
            );
            bool hasTranslation = !string.IsNullOrWhiteSpace(content?.UserTranslation);
            float linesHeight = VisibleLinesHeight(
                DialogueCinemaLayout.LinesWidth(column, hasTranslation),
                out bool hasEarlierLines
            );
            float nameWidth = Mathf.Max(
                MinNameWidth,
                npcNameText.GetPreferredValues(npcNameText.text).x + 2 * NamePadding
            );
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                lastCanvasSize,
                linesHeight,
                nameWidth,
                hasEarlierLines,
                hasTranslation
            );
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = new Vector2(1, 0);
            frame.pivot = new Vector2(.5f, 0);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(0, layout.Height);

            if (nameChip != null)
                Place(nameChip, layout.Name);
            Place(npcNameText.rectTransform, layout.Name);
            float toolsLeft = PlaceTools(layout.Tools);
            // 호감도·무례 경고는 이름표 바로 오른쪽에 두고, 도구 버튼 앞에서 말줄임으로 자른다.
            Place(
                standingText.rectTransform,
                Rect.MinMaxRect(
                    layout.Name.xMax + DialogueCinemaLayout.Gap,
                    layout.Name.yMin,
                    Mathf.Max(
                        layout.Name.xMax + DialogueCinemaLayout.Gap,
                        toolsLeft - DialogueCinemaLayout.Gap
                    ),
                    layout.Name.yMax
                )
            );
            // 대사 칸 높이가 달라져도 보던 아래쪽(보통 가장 최근 대사와 단어 풀이)이 가려지지 않게 스크롤 위치를 지킨다.
            float scroll = historyScroll.verticalNormalizedPosition;
            Place((RectTransform)historyScroll.transform, layout.Lines);
            if (translationCard != null)
            {
                if (hasTranslation)
                {
                    Place(translationCard, layout.Translation);
                    Place(translationDivider, layout.TranslationDivider);
                }
                translationDivider.gameObject.SetActive(
                    hasTranslation && layout.TranslationDivider.width > 0
                );
            }
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = scroll;
            CoverHiddenWords();
            PlaceWordCard();
            // 음성 동의처럼 긴 안내는 글자를 줄여 한 줄에 맞춘다.
            Place(statusText.rectTransform, layout.Status);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14;
            statusText.fontSizeMax = 20;
            Place((RectTransform)inputField.transform, layout.Input);
            Place((RectTransform)sendButton.transform, layout.Send);
            Place((RectTransform)talkButton.transform, layout.Talk);

            helpPanel.anchorMin = helpPanel.anchorMax = helpPanel.pivot = Vector2.one;
            helpPanel.anchoredPosition = new Vector2(-HelpMargin, -HelpTop);
            float helpRoom =
                lastCanvasSize.y - HelpTop - layout.Name.yMax - 2 * DialogueCinemaLayout.Gap;
            if (suggestions != null)
            {
                float suggestionHeight = Mathf.Clamp(helpRoom, 0, 440);
                suggestions.SetBounds(
                    new Vector2(-HelpMargin, -HelpTop),
                    Mathf.Min(HelpWidth, lastCanvasSize.x - 2 * HelpMargin),
                    suggestionHeight
                );
                if (reviewReference == null)
                {
                    PlaceVehicleStatus(layout);
                    return;
                }
                // 복습 상황 설명 아래에도 같은 힌트 카드를 두되, 두 카드의 합이 대화창 위 여유 높이를 넘지 않는다.
                float hintHeight = suggestions.ReferenceHintHeight(suggestionHeight);
                helpRoom = Mathf.Max(0, suggestionHeight - hintHeight - (hintHeight > 0 ? 12 : 0));
            }
            // 단어·문장 카드가 남는 높이보다 길면 아래가 잘려 보이지 않게 묶음째 줄여 넣는다(DialogueHelpFit).
            float room = Mathf.Clamp(helpRoom, 160, 600);
            var helpContent = (RectTransform)helpPanel.Find("Viewport/Content");
            LayoutRebuilder.ForceRebuildLayoutImmediate(helpContent);
            float scale = DialogueHelpFit.Scale(
                room,
                LayoutUtility.GetPreferredHeight(helpContent)
            );
            helpPanel.localScale = new Vector3(scale, scale, 1f);
            helpPanel.sizeDelta = new Vector2(
                Mathf.Min(HelpWidth, lastCanvasSize.x - 2 * HelpMargin),
                room / scale
            );
            PlaceVehicleStatus(layout);
            if (reviewReference != null)
            {
                helpPanel.localScale = Vector3.one;
                // 일반 도움말의 축소 비율을 되돌린 뒤 높이도 실제 여유 공간으로 되돌린다.
                // 내용 길이에 따라 창을 늘리지 않고 기존 Viewport 안에서 스크롤한다.
                helpPanel.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Clamp(helpRoom, 0, 440)
                );
                helpPanel.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    Mathf.Min(400, lastCanvasSize.x - 2 * HelpMargin)
                );
                var card = (RectTransform)suggestText.transform.parent;
                var list = card.GetComponent<VerticalLayoutGroup>();
                if (list != null)
                    list.padding = new RectOffset(12, 12, 16, 20);
                suggestText.fontSize = 24;
                suggestText.enableAutoSizing = false;
                suggestText.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    helpPanel.rect.width - 72
                );
                var size = suggestText.GetComponent<LayoutElement>();
                if (size != null)
                    size.preferredHeight =
                        suggestText
                            .GetPreferredValues(suggestText.text, helpPanel.rect.width - 72, 0)
                            .y + 12;
                LayoutRebuilder.ForceRebuildLayoutImmediate(helpContent);
            }
        }

        // 최근 네 대사와 그 뜻이 보이는 높이를 준다. 긴 내용·작은 화면과 그 이전 대사는 스크롤한다.
        private float VisibleLinesHeight(float width, out bool hasEarlierLines)
        {
            IReadOnlyList<DialogueLine> lines = content?.Lines;
            hasEarlierLines = lines != null && lines.Count > VisibleLineCount;
            if (!hasEarlierLines)
                return linesText.GetPreferredValues(linesText.text, width, 0).y;
            string latest = DialogueLinesText.Format(
                lines.Skip(lines.Count - VisibleLineCount).ToArray(),
                content.NpcName,
                isMeaningVisible,
                content.LastNpcGlossary,
                glossaryCard.Word,
                SentenceIndent(),
                HidesNpcLines
            );
            return linesText.GetPreferredValues(latest, width, 0).y;
        }

        // [다시 시도]·[대화 종료]를 이름 줄 오른쪽 끝에 두고, 나머지 도구는 그 왼쪽에 오른쪽 정렬로 붙인다.
        // 대화 종류에 따라 숨는 버튼이 있어 보이는 버튼의 폭으로 자리를 정하고, 도구가 시작하는 왼쪽 끝을 돌려준다.
        private float PlaceTools(Rect row)
        {
            var ends = (RectTransform)endButton.transform.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(ends);
            float endsWidth = LayoutUtility.GetPreferredWidth(ends);
            Place(ends, Rect.MinMaxRect(row.xMax - endsWidth, row.yMin, row.xMax, row.yMax));
            var tools = (RectTransform)meaningButton.transform.parent;
            float toolsRight = row.xMax - endsWidth - DialogueCinemaLayout.Gap;
            Place(
                tools,
                Rect.MinMaxRect(row.xMin, row.yMin, Mathf.Max(row.xMin, toolsRight), row.yMax)
            );
            LayoutRebuilder.ForceRebuildLayoutImmediate(tools);
            return toolsRight - LayoutUtility.GetPreferredWidth(tools);
        }

        private static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = area.position;
            rect.sizeDelta = area.size;
        }

        // 택시 안내는 대화창 이름 줄 바로 위 가운데에 붙인다. 화면 위쪽 가운데에 두면 왼쪽 위 목표 카드와 겹쳤다.
        // 안내 판은 대화창과 같은 캔버스 아래에 있어 대화창의 자리를 그대로 기준으로 삼는다.
        private void PlaceVehicleStatus(DialogueCinemaLayout layout)
        {
            if (vehicleStatusText == null)
                return;
            var status = (RectTransform)vehicleStatusText.transform.parent;
            status.anchorMin = status.anchorMax = new Vector2(.5f, 0);
            status.pivot = new Vector2(.5f, 0);
            status.anchoredPosition = new Vector2(0, layout.Name.yMax + VehicleStatusGap);
        }

        private bool NeedsVoiceConsent => content.UsesVoice && !content.CanTalk;
    }
}
