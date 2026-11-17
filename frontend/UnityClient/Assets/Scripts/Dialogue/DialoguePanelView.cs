using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using A305.Player;
using A305.Theme;
using A305.World;
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
    public partial class DialoguePanelView : MonoBehaviour
    {
        private const string TalkCaption = "말하기";
        private const string StopTalkCaption = "종료";

        // 녹음을 시작했지만 마이크가 아직 열리는 중. 이때 한 말은 녹음되지 않는다. 눌러서 그만둘 수는 있다.
        private const string PreparingTalkCaption = "준비 중";

        private const string EnableSubtitlesCaption = "자막 켜기";
        private const string DisableSubtitlesCaption = "자막 끄기";
        private const float EndHighlightPulseSeconds = .7f;
        private const int EndHighlightPulseCount = 2;
        private static readonly Color EndHighlightColor = new Color(1f, .8f, .32f, .55f);

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text npcNameText;

        [SerializeField]
        [Tooltip("NPC 이름 옆의 호감도와 무례 경고 칸. 서버가 보내지 않으면 숨긴다.")]
        private TMP_Text standingText;

        [SerializeField]
        [Tooltip("NPC 이름표 바로 오른쪽의 욕구 게이지. 생활하지 않는 NPC면 숨긴다.")]
        private DialogueNeedsView needsView;

        [SerializeField]
        private Button endButton;

        private Outline endButtonHighlight;
        private float endButtonHighlightStartedAt;

        [SerializeField]
        private TMP_Text linesText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private RectTransform translationCard;

        [SerializeField]
        private TMP_Text translationText;

        [SerializeField]
        [Tooltip("고친 문장·영어 번역 오른쪽의 스피커. 그 문장을 기기 영어 음성으로 읽는다.")]
        private Button translationSpeakButton;

        [SerializeField]
        private RectTransform translationDivider;

        [SerializeField]
        [Tooltip(
            "영어 표현 카드의 머리줄. 고친 영어 문장·한국어로 한 말의 영어·맞게 말함에 따라 제목이 바뀐다."
        )]
        private TMP_Text translationHeading;

        [SerializeField]
        [Tooltip(
            "카드 글자 칸(Viewport) 안에서 고친 문장과 펼친 다른 표현을 차례로 쌓는 칸. 넘치면 카드 안에서 스크롤한다."
        )]
        private RectTransform translationBody;

        [SerializeField]
        [Tooltip("다른 표현을 펼쳤을 때 목록 위의 '이렇게도 말할 수 있어요'")]
        private TMP_Text othersHeading;

        [SerializeField]
        [Tooltip("다른 표현 한 줄(번호 칸)의 본. 문장 수만큼 복제해 쓰고 본은 숨겨 둔다.")]
        private RectTransform otherRowTemplate;

        [SerializeField]
        [Tooltip("카드 머리줄의 다른 표현 펼치기·접기. 스크롤과 분리한다.")]
        private Button othersButton;

        [SerializeField]
        [Tooltip("[더 보기] 위의 가는 구분선")]
        private RectTransform othersDivider;

        /// <summary>다른 표현을 펼쳤을 때 목록 위 제목.</summary>
        public const string OthersHeading = "이렇게도 말할 수 있어요";

        /// <summary>무례한 말의 다른 표현(같은 기분을 공손하게 바꾼 말)을 펼쳤을 때 목록 위 제목.</summary>
        public const string PoliteHeading = "무례하지 않게 말하면";

        /// <summary>고친 문장 없이 다른 표현만 있을 때(맞게 말했을 때)의 카드 제목.</summary>
        public const string WellSaidHeading = "잘 말했어요";

        private const float CardBottomPadding = 20;

        private readonly List<RectTransform> otherRows = new List<RectTransform>();
        private ScrollRect translationScroll;

        // Awake 전에 내용을 받을 수 있어(Editor 미리보기·비활성 창) 처음 쓸 때 찾는다.
        private ScrollRect TranslationScroll
        {
            get
            {
                if (translationScroll == null)
                    translationScroll = translationCard.GetComponent<ScrollRect>();
                return translationScroll;
            }
        }

        private IReadOnlyList<string> shownOthers;
        private bool othersOpen;
        private bool othersButtonTinted;

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

        // 참고 카드 제목. 비우면 복습 제목(이번 복습·이번에 써 볼 표현)을 쓴다.
        private string reviewHeading;
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
            {
                reviewScenario = null;
                reviewHeading = null;
            }
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
            string scenario = null,
            string heading = null,
            string countLabel = "말해봤어요",
            bool listFirst = false
        )
        {
            reviewScenario = scenario;
            reviewHeading = heading;
            ReviewReferenceText formatted = ReviewReferenceText.Format(
                items,
                verifiedCount,
                scenario,
                countLabel,
                listFirst
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
        private Button performanceButton;
        private bool performanceVisible;
        private bool performanceEnabled;
        private bool performancePlaying;
        private bool performanceEmphasized;
        private string performanceLyrics;

        public event Action PerformanceRequested;

        public void SetPerformanceState(
            bool visible,
            bool interactable,
            bool playing,
            bool emphasized,
            string lyrics = null
        )
        {
            if (performanceButton == null)
                return;
            if (
                performanceVisible == visible
                && performanceEnabled == interactable
                && performancePlaying == playing
                && performanceEmphasized == emphasized
                && (lyrics == null || performanceLyrics == lyrics)
            )
                return;
            performanceVisible = visible;
            performanceEnabled = interactable;
            performancePlaying = playing;
            performanceEmphasized = emphasized;
            performanceLyrics = visible ? lyrics ?? performanceLyrics : null;
            performanceButton.gameObject.SetActive(visible);
            performanceButton.interactable = interactable;
            performanceButton.GetComponentInChildren<TMP_Text>().text = playing
                ? "랩 멈추기"
                : "랩 다시 듣기";
            var outline = performanceButton.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = emphasized;
            Refresh();
        }

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

        // 대사·뜻·가사의 실제 줄바꿈 기준 상한.
        private const int MaxVisibleTextLines = 5;

        /// <summary>녹음 중 목소리가 들릴 때 [종료] 버튼의 불 색.</summary>
        public static readonly Color VoiceHeardColor = new Color32(76, 217, 100, 255);
        private static readonly Color VoiceQuietColor = new Color(1f, 1f, 1f, 0.55f);

        // 자유 대화의 입력칸 안내("영어로 답하기…"). 정해진 대사를 따라가는 대화에서는 그 대사로 바꿔 보인다.
        private string freePlaceholder;

        /// <summary>대사와 화자 이름을 함께 밝히는 튜토리얼 강조 영역.</summary>
        public IReadOnlyList<RectTransform> DialogueLinesParts =>
            new[] { LinesRect, npcNameText.rectTransform };

        /// <summary>
        /// 정해진 대사를 말하거나 입력하는 입력칸·[보내기]·[말하기]. 튜토리얼 코치 마크가 한 구멍으로 밝힌다.
        /// [힌트]는 오른쪽 추천 문장 카드에 있어 함께 담으면 구멍이 화면 반을 덮으므로 넣지 않는다.
        /// </summary>
        public IReadOnlyList<RectTransform> GuidedLineParts
        {
            get
            {
                var parts = new List<RectTransform>
                {
                    (RectTransform)inputField.transform,
                    (RectTransform)sendButton.transform,
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

        /// <summary>대화창 도구 줄의 [자막]·[다시 듣기]·[뜻] 중 보이는 버튼.</summary>
        public IReadOnlyList<RectTransform> ToolParts
        {
            get
            {
                var parts = new List<RectTransform>();
                foreach (Button button in new[] { subtitleButton, replayButton, meaningButton })
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

        /// <summary>오른쪽 추천 문장 카드와 그 안의 [힌트]. 튜토리얼 코치 마크가 밝힌다.</summary>
        public RectTransform SuggestedSentenceRect =>
            suggestions != null ? suggestions.HintRect : (RectTransform)hintButton.transform;

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

        /// <summary>영어 표현 카드에서 문장 옆 스피커를 누르면 그 문장(고친 문장·영어 번역·다른 표현)을 알린다.</summary>
        public event Action<string> ExpressionListenRequested;

        /// <summary>[뜻 보기]로 뜻을 펼쳤는데 아직 뜻이 없으면 알린다. 서버 대화는 이때 뜻을 받아 온다.</summary>
        public event Action MeaningRequested;

        /// <summary>추천 문장을 아직 받지 않았을 때 [힌트]를 누르면 알린다. 받은 뒤에는 묻지 않고 그 문장을 입력창에 다시 넣는다.</summary>
        public event Action HintRequested;
        public event Action HintOpened;

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
            SetEndButtonHighlight(false);
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
            CloseExpressionWindow();
            SetPerformanceState(false, false, false, false);
            SetEndButtonHighlight(false);
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
            // 복습·장소 연습 대화는 피드백 없이 연습 결과만 받으므로 나중에 확인할 것이 없다.
            if (feedbackLaterButton != null)
                feedbackLaterButton.gameObject.SetActive(
                    !(content is ServerConversation { IsReview: true })
                        && !(content is ServerConversation { IsPractice: true })
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
                suggestions.gameObject.SetActive(!reviewing);
                helpPanel.gameObject.SetActive(reviewing);
                suggestions.Show(
                    content.SuggestWords,
                    content.IsSuggestingWords,
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
                reviewHelpTitle.text =
                    !reviewing ? originalHelpTitle
                    : reviewHeading != null ? reviewHeading
                    : reviewScenario == null ? "이번에 써 볼 표현"
                    : "이번 복습";
            if (reviewFoldButton != null)
                reviewFoldButton.gameObject.SetActive(reviewing);

            sendButton.interactable = content.CanSend;
            // 정해진 대사를 따라가는 대화(튜토리얼 길 안내)는 대사를 다 마칠 때까지 끝낼 수 없다.
            endButton.interactable = !content.IsGuided || content.IsGuidedCompleted;
            SetEndButtonHighlight(
                statusText.gameObject.activeSelf
                    && endButton.IsInteractable()
                    && content is ServerConversation { ShowsEndFeedbackNotice: true }
            );
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
            bool preparingOpening =
                content.SpeaksNpcLines
                && (
                    content.Waiting == DialogueWait.Opening
                    || (content.LastNpcLine == null && !content.CanRetry)
                );
            replayButton.gameObject.SetActive(preparingOpening || content.CanReplaySpeech);
            replayButton.interactable =
                content.CanReplaySpeech && content.LastNpcLine != null && !content.IsBusy;
            hintButton.gameObject.SetActive(content.HasHint);
            hintButton.interactable = content.LastNpcLine != null && content.CanSend;
            retryButton.gameObject.SetActive(content.CanRetry);
            ShowSubtitleButton();
            UpdateLayout();
            Canvas.ForceUpdateCanvases();
            // 대사가 쌓이면 가장 최근 대사와 뜻이 보이게 맨 아래로 내린다. 지난 대사는 위로 스크롤해 다시 본다.
            historyScroll.verticalNormalizedPosition =
                historyScroll.content.rect.height <= historyScroll.viewport.rect.height + 1 ? 1 : 0;
        }

        private string pronunciationNotice;
        private string shownTranslation;

        /// <summary>한국어로(또는 섞어서) 한 말의 영어 번역에 붙이는 카드 제목.</summary>
        public const string TranslationHeading = "영어로는 이렇게 말해요";

        /// <summary>영어로 한 말의 교정문에 붙이는 카드 제목. 지금 다시 말하라고 재촉하지 않고 알려 주기만 한다.</summary>
        public const string CorrectionHeading = "이렇게 말하면 자연스러워요";

        // AI 워커는 한글이 든 말을 교정하지 않고 번역만 하므로, 같은 기준으로 번역과 교정을 나눈다.
        private static readonly Regex Hangul = new Regex("[가-힣ㄱ-ㅎㅏ-ㅣ]");

        public void ShowPronunciationFailure()
        {
            pronunciationNotice =
                "영어 음성을 재생할 수 없어요. 기기의 영어 음성과 소리 설정을 확인해 주세요.";
            Refresh();
        }

        // 서버가 준 같은 뜻의 다른 표현(최대 3개). 고친 문장·영어 번역과 함께 와도 카드 아래에 접어 둔다.
        private IReadOnlyList<string> OtherExpressions =>
            content?.UserBetterExpressions?.Count > 0 ? content.UserBetterExpressions : null;

        // 대화창 옆 영어 표현 카드를 띄울지. 고친 문장·영어 번역이나 다른 표현 중 하나가 있으면 띄운다.
        private bool HasExpressionCard =>
            !string.IsNullOrWhiteSpace(content?.UserTranslation) || OtherExpressions != null;

        /// <summary>
        /// 영어 표현 카드(S15P31A305-862·895). 위에는 상황별 제목과, 고친 문장·영어 번역이 있으면 바뀐 곳을 표시한 전체 문장을 둔다.
        /// 다른 표현이 있으면 늘 접어 두고, 머리줄의 [다른 표현 N개 ›]로 펼치면 문장마다 번호 칸으로 나눠 보인다.
        /// 영어 문장마다 오른쪽 스피커로 그 문장을 듣는다. 무례한 말의 다른 표현은 공손하게 바꾼 말이라 제목을 바꾼다.
        /// </summary>
        private void ShowTranslation()
        {
            if (translationCard == null)
                return;
            string expression = content?.UserTranslation;
            IReadOnlyList<string> others = OtherExpressions;
            if (shownTranslation != expression)
                pronunciationNotice = null;
            // 다음 말을 하면 다른 표현은 다시 접고 맨 위부터 보인다.
            if (shownTranslation != expression || !ReferenceEquals(shownOthers, others))
            {
                CloseExpressionWindow();
                othersOpen = false;
                translationBody.anchoredPosition = Vector2.zero;
            }
            shownTranslation = expression;
            shownOthers = others;
            bool hasExpression = !string.IsNullOrWhiteSpace(expression);
            bool hasOthers = others != null;
            translationCard.gameObject.SetActive(hasExpression || hasOthers);

            string said = content?.Lines?.LastOrDefault(line => !line.IsNpc)?.English;
            translationHeading.text =
                !hasExpression ? WellSaidHeading
                : IsCorrection() ? CorrectionHeading
                : TranslationHeading;
            translationText.transform.parent.gameObject.SetActive(hasExpression);
            translationText.text = hasExpression
                ? ExpressionChanges.Mark(said, expression)
                : string.Empty;
            translationSpeakButton.interactable = hasExpression && !content.IsBusy;

            othersButton.gameObject.SetActive(hasOthers);
            // 다른 표현이 있다는 걸 알아보게 [자막 끄기]처럼 유리 버튼 테두리를 강조색으로 칠한다(모양은 그대로).
            if (!othersButtonTinted && othersButton.TryGetComponent(out ThemedElement othersTheme))
            {
                othersTheme.SetSelected(true, keepGhostOutline: true);
                othersButtonTinted = true;
            }
            othersDivider.gameObject.SetActive(hasOthers);
            othersButton.GetComponentInChildren<TMP_Text>(true).text = othersOpen
                ? "접기"
                : $"다른 표현 {others?.Count ?? 0}개";
            // 꺾쇠는 접혀 있으면 오른쪽(›), 펼치면 아래(⌄)를 가리킨다.
            othersButton.transform.Find("Chevron").localRotation = Quaternion.Euler(
                0,
                0,
                othersOpen ? -90 : 0
            );
            othersHeading.text = content.UserTurnRude ? PoliteHeading : OthersHeading;
            ApplyExpressionHeader(hasOthers);
            ShowOthers(hasOthers && othersOpen);
            TranslationScroll.content = translationBody;
        }

        // 다른 표현 목록을 펼치거나 접는다. 펼치면 제목 아래 문장마다 번호 칸 한 줄로 보인다.
        private void ShowOthers(bool open)
        {
            IReadOnlyList<string> sentences = open
                ? OtherExpressions ?? Array.Empty<string>()
                : Array.Empty<string>();
            othersHeading.gameObject.SetActive(sentences.Count > 0);
            ShowOtherRows(translationBody, otherRows, sentences);
        }

        // 다른 표현 줄을 본에서 복제해 두고 다시 쓴다. 줄마다 스피커가 그 줄의 문장을 읽는다.
        private void ShowOtherRows(
            RectTransform body,
            List<RectTransform> rows,
            IReadOnlyList<string> sentences
        )
        {
            while (rows.Count < sentences.Count)
            {
                RectTransform row = Instantiate(otherRowTemplate, body);
                int index = rows.Count;
                row.name = $"Other {index + 1}";
                row.Find("SpeakButton")
                    .GetComponent<Button>()
                    .onClick.AddListener(() =>
                        ListenTo(OtherExpressions?.ElementAtOrDefault(index))
                    );
                rows.Add(row);
            }
            for (int index = 0; index < rows.Count; index++)
            {
                bool shown = index < sentences.Count;
                rows[index].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                rows[index].Find("Number/Label").GetComponent<TMP_Text>().text = (
                    index + 1
                ).ToString();
                rows[index].Find("Text").GetComponent<TMP_Text>().text =
                    $"<noparse>{sentences[index]}</noparse>";
                rows[index].Find("SpeakButton").GetComponent<Button>().interactable =
                    !content.IsBusy;
            }
        }

        // 카드의 영어 문장 하나를 기기 음성으로 듣게 알린다. 대답을 기다리는 동안은 읽지 않는다.
        private void ListenTo(string sentence)
        {
            if (!string.IsNullOrWhiteSpace(sentence) && content != null && !content.IsBusy)
                ExpressionListenRequested?.Invoke(sentence);
        }

        // 머리줄의 다른 표현·접기. 펼치거나 접을 때마다 맨 위부터 보인다.
        private void ToggleOtherExpressions()
        {
            othersOpen = !othersOpen;
            translationBody.anchoredPosition = Vector2.zero;
            Refresh();
        }

        private bool IsCorrection()
        {
            DialogueLine said = content.Lines?.LastOrDefault(line => !line.IsNpc);
            return said != null && !Hangul.IsMatch(said.English ?? string.Empty);
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
            if (performanceButton != null)
                performanceButton.onClick.AddListener(() => PerformanceRequested?.Invoke());
            if (translationSpeakButton != null)
                translationSpeakButton.onClick.AddListener(() =>
                    ListenTo(content?.UserTranslation)
                );
            if (othersButton != null)
                othersButton.onClick.AddListener(ToggleOtherExpressions);
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

        private void PressHint()
        {
            if (content == null)
            {
                return;
            }

            // 튜토리얼도 서비스와 같이 추천 문장을 펼치기만 하고, 펼친 문장을 눌러야 입력칸에 넣는다.
            hintVisible = !hintVisible;
            if (hintVisible && !string.IsNullOrEmpty(content.HintText))
                HintOpened?.Invoke();
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

        // 음성보다 글자가 먼저 올 수 있으므로 수신 중에도 자막을 가린다. 수신이 끝나도 들을 음성이 없으면 글자를 보인다(F-25).
        private bool HidesNpcLines =>
            content.SpeaksNpcLines
            && (content.IsReceivingNpcReply || content.IsNpcSpeechReady)
            && !NpcSubtitlePreference.ShowsSubtitles;

        private void ShowSubtitleButton()
        {
            subtitleButton.gameObject.SetActive(content.SpeaksNpcLines);
            bool showsSubtitles = NpcSubtitlePreference.ShowsSubtitles;
            subtitleButton.GetComponentInChildren<TMP_Text>(true).text = showsSubtitles
                ? DisableSubtitlesCaption
                : EnableSubtitlesCaption;
            if (subtitleButton.TryGetComponent(out ThemedElement theme))
                theme.SetSelected(showsSubtitles, keepGhostOutline: true);
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
            if (HasExpressionCard)
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

        // 대화 중 연 단어 카드는 ESC 목록에서 대화보다 위라, ESC가 대화보다 카드를 먼저 닫는다.
        private UiEscapeWindow wordCardEscape;

        private void LateUpdate()
        {
            if (content == null)
                return;
            wordCardEscape ??= new UiEscapeWindow(
                () => wordCard.gameObject.activeInHierarchy,
                CloseWordCard
            );
            wordCardEscape.Watch();
            // 변화량과 경고 문구는 몇 초 뒤 새 상태가 없어도 사라져야 하므로 매 프레임 다시 고른다.
            UpdateStanding();
            UpdateEndButtonHighlight();
            if (((RectTransform)panel.transform.parent).rect.size != lastCanvasSize)
                UpdateLayout();
        }

        private void SetEndButtonHighlight(bool visible)
        {
            if (endButtonHighlight == null)
            {
                if (!visible || endButton.targetGraphic == null)
                    return;

                endButtonHighlight = endButton.targetGraphic.gameObject.AddComponent<Outline>();
                endButtonHighlight.effectDistance = new Vector2(2f, -2f);
                endButtonHighlight.useGraphicAlpha = false;
                endButtonHighlight.enabled = false;
            }

            if (endButtonHighlight.enabled == visible)
                return;

            if (visible)
            {
                endButtonHighlightStartedAt = Time.unscaledTime;
                endButtonHighlight.effectColor = EndHighlightColor;
            }
            endButtonHighlight.enabled = visible;
        }

        private void UpdateEndButtonHighlight()
        {
            if (endButtonHighlight == null || !endButtonHighlight.enabled)
                return;

            // 두 번만 반짝이고, 누르기 전까지는 눈에 띄는 테두리를 유지한다.
            float elapsed = Time.unscaledTime - endButtonHighlightStartedAt;
            float pulse = 0f;
            if (elapsed < EndHighlightPulseSeconds * EndHighlightPulseCount)
                pulse = .5f - .5f * Mathf.Cos(2f * Mathf.PI * elapsed / EndHighlightPulseSeconds);

            Color color = EndHighlightColor;
            color.a = Mathf.Lerp(color.a, .95f, pulse);
            if (endButtonHighlight.effectColor != color)
                endButtonHighlight.effectColor = color;
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

        /// <summary>
        /// 대화하는 NPC의 욕구 게이지를 보인다. 대화를 시작한 순간의 값이며, 대화 중에는 게임 시간이 멈춰 바뀌지 않는다.
        /// </summary>
        /// <param name="needs">생활하지 않는 NPC(튜토리얼 Ben, 택시 기사, 출퇴근 중인 몸 등)면 null이며 게이지를 숨긴다.</param>
        public void ShowNeeds(NpcNeeds needs)
        {
            if (needsView == null)
                return;
            needsView.Show(NpcNeedGauges.From(needs));
            // 대화를 시작하기 전(창이 닫혀 있을 때)이면 대화 창을 열 때 자리를 정한다.
            if (panel.activeSelf && content != null)
                UpdateLayout();
        }

        // 서버가 무례 한도를 보내기 전에 쓰는 기본 한도. 서버 기본값과 같다(docs/api/rudeness-handling.md).
        private const int DefaultRudeLimit = 3;

        // 욕구 게이지를 이름표(와 공연 버튼) 바로 오른쪽에 두고, 호감도·무례 경고가 시작할 왼쪽 끝을 돌려준다.
        // 자리는 이름 길이로만 정해져 무례 경고 문구가 붙었다 사라져도 움직이지 않는다.
        // 게이지 뒤에 가장 긴 호감 줄(무례 경고 문구가 붙은 때)이 들어가지 않는 좁은 화면이면 경고가 잘리지 않게 게이지를 숨긴다.
        private float PlaceNeeds(float left, float toolsLeft, Rect nameRow)
        {
            if (needsView == null)
                return left;
            int rudeLimit =
                content?.Standing != null && content.Standing.IsKnown
                    ? content.Standing.RudeLimit
                    : DefaultRudeLimit;
            float widestStanding = standingText
                .GetPreferredValues(ConversationStandingDisplay.Widest(rudeLimit))
                .x;
            bool fits =
                needsView.Width > 0
                && DialogueCinemaLayout.FitsBesideName(
                    left,
                    toolsLeft,
                    needsView.Width,
                    widestStanding
                );
            needsView.gameObject.SetActive(fits);
            if (!fits)
                return left;
            Place(
                (RectTransform)needsView.transform,
                new Rect(left, nameRow.yMin, needsView.Width, nameRow.height)
            );
            return left + needsView.Width + DialogueCinemaLayout.Gap;
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

        // 단어·추천 문장 카드에 남겨 둘 높이. 펼친 추천 문장 한 줄과 그 뜻이 잘리지 않고 다 보이는 높이다.
        // 이보다 많이 눌리면 [힌트]가 추천 문장 카드 밖으로 나가고 단어가 한 줄만 보인다.
        private const float MinSuggestionsHeight = 494;

        // 대사가 짧아 자리가 남을 때 두 카드가 커지는 상한
        private const float MaxSuggestionsHeight = 534;

        // 단어·추천 문장 카드 자리. 대화 중에는 오른쪽 위 휴대폰·시계·코인이 숨으므로 화면 위·오른쪽 여백부터 쓴다.
        private const float SuggestionsTop = HudTopBarLayout.Margin;
        private const float SuggestionsRight = HudTopBarLayout.Margin;

        [SerializeField]
        private TMP_Text vehicleStatusText;

        [SerializeField]
        private RectTransform taxiArrivalStatus;

        [SerializeField]
        [Tooltip("NPC 이름 뒤의 코랄 이름표. 이름 길이에 맞춰 폭을 정한다.")]
        private RectTransform nameChip;

        /// <summary>택시에 탄 동안의 대화인지. 택시 안내는 택시에서만 보인다.</summary>
        public void SetVehicleLayout(bool enabled)
        {
            if (!enabled)
            {
                if (vehicleStatusText != null)
                    vehicleStatusText.transform.parent.gameObject.SetActive(false);
                if (taxiArrivalStatus != null)
                    taxiArrivalStatus.gameObject.SetActive(false);
            }
            UpdateLayout();
        }

        public void SetVehicleStatus(string message)
        {
            if (vehicleStatusText == null)
                return;
            vehicleStatusText.text = message;
            vehicleStatusText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void ShowTaxiArrival()
        {
            SetVehicleStatus(null);
            if (taxiArrivalStatus == null)
                return;
            PlaceTaxiArrivalStatus(((RectTransform)taxiArrivalStatus.parent).rect.size);
            taxiArrivalStatus.SetAsLastSibling();
            taxiArrivalStatus.gameObject.SetActive(true);
        }

        /// <summary>
        /// 대화창을 화면 아래 전체 폭에 깔고, 가운데 열에 입력 줄·상태 줄·대사·이름 줄을 쌓는다.
        /// 단어·문장 도움 카드는 오른쪽 위에 둔다.
        /// </summary>
        public void UpdateLayout()
        {
            var frame = (RectTransform)panel.transform;
            lastCanvasSize = ((RectTransform)frame.parent).rect.size;
            PlaceTaxiArrivalStatus(lastCanvasSize);
            float column = Mathf.Min(
                DialogueCinemaLayout.ColumnWidth,
                lastCanvasSize.x - 2 * DialogueCinemaLayout.SideMargin
            );
            bool hasTranslation = HasExpressionCard;
            float linesHeight = VisibleLinesHeight(
                DialogueCinemaLayout.LinesWidth(column, hasTranslation),
                out bool hasEarlierLines
            );
            if (performanceVisible && !string.IsNullOrEmpty(performanceLyrics))
            {
                // 짧은 랩도 반복할 때 창이 커지지 않게 한 곡 높이를 유지한다.
                // 긴 가사는 위에서 계산한 다섯 줄 상한을 그대로 적용한다.
                string onePerformance = DialogueLinesText.Format(
                    new[] { DialogueLine.FromNpc(performanceLyrics, null) },
                    content.NpcName,
                    false,
                    null,
                    sentenceIndent: SentenceIndent()
                );
                float performanceHeight = linesText
                    .GetPreferredValues(
                        onePerformance,
                        DialogueCinemaLayout.LinesWidth(column, hasTranslation),
                        0
                    )
                    .y;
                linesHeight = Mathf.Min(linesHeight, performanceHeight);
            }
            float nameWidth = Mathf.Max(
                MinNameWidth,
                npcNameText.GetPreferredValues(npcNameText.text).x + 2 * NamePadding
            );
            // 대화창은 다른 표현을 펼쳤을 때의 카드 높이로 잡아, 접거나 펼쳐도 커지거나 줄지 않는다.
            // 카드는 그 자리 위쪽에 붙여 지금 보이는 내용만큼 그리므로 펼치면 아래로만 늘어난다.
            float cardWidth = DialogueCinemaLayout.TranslationWidth(column);
            float reservedCard = ExpressionCardHeight(cardWidth, OtherExpressions != null);
            float shownCard = ExpressionCardHeight(cardWidth, othersOpen);
            // 오른쪽 위 단어·추천 문장 카드 자리. 대사와 영어 표현 카드는 이 자리를 넘지 않는 높이까지만 키운다.
            // 복습·연습 중에는 말해 볼 문장을 알려 주는 참고 카드 자리를 먼저 남긴다.
            float reviewCardHeight = reviewReference != null ? FitReviewCard() : 0;
            float roomAboveName =
                reviewReference != null
                    ? ReviewCardTop
                        + Mathf.Min(reviewCardHeight, ReviewCardReservedHeight)
                        + 2 * DialogueCinemaLayout.Gap
                : suggestions != null
                    ? SuggestionsTop + MinSuggestionsHeight + 2 * DialogueCinemaLayout.Gap
                : 0;
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                lastCanvasSize,
                linesHeight,
                nameWidth,
                hasEarlierLines,
                hasTranslation,
                reservedCard,
                roomAboveName,
                shownCard
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
            float standingLeft = layout.Name.xMax + DialogueCinemaLayout.Gap;
            if (performanceButton != null && performanceVisible)
            {
                const float width = 205;
                bool fits = toolsLeft - standingLeft >= width + DialogueCinemaLayout.Gap;
                Place(
                    (RectTransform)performanceButton.transform,
                    new Rect(
                        fits ? standingLeft : layout.Name.xMin,
                        fits ? layout.Name.yMin : layout.Name.yMax + DialogueCinemaLayout.Gap,
                        width,
                        DialogueCinemaLayout.NameHeight
                    )
                );
                if (fits)
                    standingLeft += width + DialogueCinemaLayout.Gap;
            }
            standingLeft = PlaceNeeds(standingLeft, toolsLeft, layout.Name);
            // 호감도·무례 경고는 욕구 게이지(없으면 이름표) 바로 오른쪽에 두고, 도구 버튼 앞에서 말줄임으로 자른다.
            Place(
                standingText.rectTransform,
                Rect.MinMaxRect(
                    standingLeft,
                    layout.Name.yMin,
                    Mathf.Max(standingLeft, toolsLeft - DialogueCinemaLayout.Gap),
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
            AlignDialogueContent();
            historyScroll.verticalNormalizedPosition =
                historyScroll.content.rect.height <= historyScroll.viewport.rect.height + 1
                    ? 1
                    : scroll;
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
            if (suggestions != null && reviewReference == null)
            {
                float suggestionsRoom =
                    lastCanvasSize.y
                    - SuggestionsTop
                    - layout.Name.yMax
                    - 2 * DialogueCinemaLayout.Gap;
                suggestions.SetBounds(
                    new Vector2(-SuggestionsRight, -SuggestionsTop),
                    Mathf.Min(HelpWidth, lastCanvasSize.x - 2 * SuggestionsRight),
                    Mathf.Clamp(suggestionsRoom, 0, MaxSuggestionsHeight)
                );
                PlaceVehicleStatus(layout);
                return;
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
                // 일반 도움말의 축소 비율과 크기를 되돌린다. 카드는 대화창 위 남은 높이 안에서 내용만큼 커지고,
                // 넘치는 내용은 카드 안에서 스크롤한다.
                FitReviewCard();
                helpPanel.anchoredPosition = new Vector2(-HelpMargin, -ReviewCardTop);
                helpPanel.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Clamp(
                        lastCanvasSize.y
                            - ReviewCardTop
                            - layout.Name.yMax
                            - 2 * DialogueCinemaLayout.Gap,
                        0,
                        reviewCardHeight
                    )
                );
            }
        }

        // 복습·연습 참고 카드 자리. 대화 중에는 오른쪽 위 HUD가 숨으므로 화면 위 여백부터 쓴다.
        private const float ReviewCardTop = HudTopBarLayout.Margin;
        private const float ReviewCardWidth = 520;

        // 대사와 영어 표현 카드가 길어져도 참고 카드에 남겨 두는 높이. 고친 문장 두 개와 '내가 한 말'이 보이는 높이다.
        private const float ReviewCardReservedHeight = 520;

        // 참고 카드의 폭과 글자 줄바꿈을 맞추고, 내용을 다 보이는 데 필요한 카드 높이를 준다.
        private float FitReviewCard()
        {
            helpPanel.anchorMin = helpPanel.anchorMax = helpPanel.pivot = Vector2.one;
            helpPanel.localScale = Vector3.one;
            helpPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                // 세로 화면처럼 좁은 화면에서는 가운데 NPC 얼굴을 덮지 않게 화면 폭의 45%까지만 쓴다.
                Mathf.Min(ReviewCardWidth, lastCanvasSize.x * .45f)
            );
            var card = (RectTransform)suggestText.transform.parent;
            var list = card.GetComponent<VerticalLayoutGroup>();
            if (list != null)
                list.padding = new RectOffset(12, 12, 16, 20);
            suggestText.fontSize = 24;
            suggestText.enableAutoSizing = false;
            float textWidth = helpPanel.rect.width - 72;
            suggestText.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                textWidth
            );
            var size = suggestText.GetComponent<LayoutElement>();
            if (size != null)
                size.preferredHeight =
                    suggestText.GetPreferredValues(suggestText.text, textWidth, 0).y + 12;
            var helpContent = (RectTransform)helpPanel.Find("Viewport/Content");
            LayoutRebuilder.ForceRebuildLayoutImmediate(helpContent);
            // 카드 안 Viewport의 위아래 여백(20씩)을 더한다.
            return LayoutUtility.GetPreferredHeight(helpContent) + 40;
        }

        // 카드가 내용을 다 보이는 데 필요한 높이. 카드 폭을 먼저 맞춘 뒤, 다른 표현을 펼친(open) 또는 접은 상태로
        // 고친 문장·다른 표현의 줄바꿈을 잰다. 빈 칸을 미리 두지 않아 '잘 말했어요'는 머리줄 높이만 차지한다.
        // 대화창 배치가 이 높이를 대사 칸 최대 높이 안으로 줄이고, 넘치는 내용은 카드 안에서 스크롤한다.
        // 재고 나면 목록을 지금 펼친 상태로 되돌린다.
        private float ExpressionCardHeight(float cardWidth, bool open)
        {
            if (translationCard == null || !translationCard.gameObject.activeSelf)
                return DialogueCinemaLayout.TranslationHeight;
            translationCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
            var viewport = (RectTransform)translationBody.parent;
            float chrome = translationCard.rect.height - viewport.rect.height;
            ShowOthers(open && OtherExpressions != null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(translationBody);
            float height = chrome + LayoutUtility.GetPreferredHeight(translationBody);
            ShowOthers(othersOpen && OtherExpressions != null);
            return height;
        }

        // 발화 수가 아니라 실제 글꼴·폭으로 줄바꿈된 다섯 줄을 상한으로 삼는다.
        // 뜻·가사도 같은 제한을 쓰며, 전체 기록은 잘라내지 않고 ScrollRect에 남긴다.
        private float VisibleLinesHeight(float width, out bool hasEarlierLines)
        {
            // 스크롤바 안쪽 여백까지 적용된 실제 폭에서 먼저 줄바꿈을 계산한다.
            ((RectTransform)historyScroll.transform).SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                width
            );
            Canvas.ForceUpdateCanvases();
            linesText.ForceMeshUpdate();
            TMP_TextInfo info = linesText.textInfo;
            hasEarlierLines = info.lineCount > MaxVisibleTextLines;
            if (!hasEarlierLines)
                return linesText.preferredHeight;

            // 뜻은 영어보다 작은 글씨다. 어느 위치로 스크롤해도 여섯 줄이 한꺼번에
            // 들어오지 않도록 다섯 줄 구간 중 가장 짧은 높이로 제한한다.
            float height = float.PositiveInfinity;
            for (int first = 0; first <= info.lineCount - MaxVisibleTextLines; first++)
            {
                int last = first + MaxVisibleTextLines - 1;
                height = Mathf.Min(
                    height,
                    info.lineInfo[first].ascender - info.lineInfo[last].descender
                );
            }
            return Mathf.Max(0, height);
        }

        private const float ToolButtonPadding = 24f;

        // [다시 시도]·[대화 종료]를 이름 줄 오른쪽 끝에 두고, 나머지 도구는 그 왼쪽에 오른쪽 정렬로 붙인다.
        // 대화 종류에 따라 숨는 버튼이 있어 보이는 버튼의 폭으로 자리를 정하고, 도구가 시작하는 왼쪽 끝을 돌려준다.
        private float PlaceTools(Rect row)
        {
            var ends = (RectTransform)endButton.transform.parent;
            var tools = (RectTransform)meaningButton.transform.parent;
            FitToolButtonsToLabels(ends);
            FitToolButtonsToLabels(tools);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ends);
            float endsWidth = LayoutUtility.GetPreferredWidth(ends);
            Place(ends, Rect.MinMaxRect(row.xMax - endsWidth, row.yMin, row.xMax, row.yMax));
            float toolsRight =
                row.xMax - endsWidth - tools.GetComponent<HorizontalLayoutGroup>().spacing;
            Place(
                tools,
                Rect.MinMaxRect(row.xMin, row.yMin, Mathf.Max(row.xMin, toolsRight), row.yMax)
            );
            LayoutRebuilder.ForceRebuildLayoutImmediate(tools);
            return toolsRight - LayoutUtility.GetPreferredWidth(tools);
        }

        private void FitToolButtonsToLabels(RectTransform row)
        {
            foreach (Transform button in row)
            {
                if (
                    !button.gameObject.activeSelf || !button.TryGetComponent(out LayoutElement size)
                )
                    continue;

                TMP_Text label = button.Find("Label").GetComponent<TMP_Text>();
                float labelWidth = label.GetPreferredValues(label.text).x;
                // 두 문구 중 긴 쪽을 기준으로 잡아 자막을 켜고 끌 때 옆 버튼도 움직이지 않게 한다.
                if (button == subtitleButton.transform)
                    labelWidth = Mathf.Max(
                        label.GetPreferredValues(EnableSubtitlesCaption).x,
                        label.GetPreferredValues(DisableSubtitlesCaption).x
                    );
                size.minWidth = size.preferredWidth = labelWidth + 2 * ToolButtonPadding;
            }
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

        private void PlaceTaxiArrivalStatus(Vector2 canvasSize)
        {
            if (taxiArrivalStatus == null)
                return;
            bool portrait = canvasSize.x < canvasSize.y;
            taxiArrivalStatus.anchoredPosition = new Vector2(
                0,
                -canvasSize.y * (portrait ? .12f : .075f)
            );
            taxiArrivalStatus.sizeDelta = new Vector2(Mathf.Clamp(canvasSize.x - 32, 0, 370), 62);
        }

        private bool NeedsVoiceConsent => content.UsesVoice && !content.CanTalk;
    }
}
