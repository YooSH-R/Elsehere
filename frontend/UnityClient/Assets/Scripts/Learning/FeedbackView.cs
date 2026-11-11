using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Player;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤의 피드백을 요약·문장 교정·전체 대화 탭으로 나누고 고정 영역 안에서 세로 스크롤로 보여 준다(확정 시안 F1·F2).
    /// 요약 탭은 요약 카드와 "이 상황에서 자주 써요" 표현 줄, 문장 교정 탭은 발화마다 카드를 두고 담을 문장마다 [담기]를 둔다.
    /// 피드백을 받지 못했으면 평가를 지어내지 않고 [다시 만들기]와 [대화 기록 보기]를 보여 준다(기능 명세 F-32).
    /// </summary>
    public class FeedbackView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text summaryText;

        [SerializeField]
        [Tooltip("받지 못한 피드백·다시 만드는 중·빈 탭의 안내 글")]
        private TMP_Text bodyText;

        [SerializeField]
        private Button detailButton;

        [SerializeField]
        private TMP_Text detailButtonLabel;

        [SerializeField]
        private Button vocabularyButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button topCloseButton;

        [SerializeField]
        [Tooltip("머리줄 X 왼쪽의 확대 버튼")]
        private Button expandButton;

        [SerializeField]
        private Sprite expandSprite;

        [SerializeField]
        private Sprite collapseSprite;

        [SerializeField]
        private Button regenerateButton;

        [SerializeField]
        private Button transcriptButton;

        [SerializeField]
        private TMP_Text transcriptButtonLabel;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button[] sectionButtons;

        [SerializeField]
        private TMP_Text pageTitleText;

        [SerializeField]
        private TMP_Text pageNumberText;

        [SerializeField]
        private TMP_Text noticeText;

        [SerializeField]
        private RectTransform sectionRow;

        [SerializeField]
        private RectTransform navigationRow;

        [SerializeField]
        [Tooltip("요약 탭의 요약 글과 잘했어요·더 해 봐요 메모")]
        private RectTransform summaryCard;

        [SerializeField]
        [Tooltip("요약 탭의 '이 상황에서 자주 써요' 제목(Header)과 표현 줄 묶음")]
        private RectTransform situationRows;

        [SerializeField]
        [Tooltip("표현 줄 원본. 숨겨 두고 표현마다 복사해 쓴다")]
        private GameObject situationRowTemplate;

        [SerializeField]
        [Tooltip("문장 교정 카드 원본. 숨겨 두고 발화마다 복사해 쓴다")]
        private RectTransform correctionCardTemplate;

        [SerializeField]
        private Sprite pickSprite;

        [SerializeField]
        private Sprite pickedSprite;

        [SerializeField]
        private Sprite saidChipSprite;

        [SerializeField]
        private Sprite saidKoreanChipSprite;

        [SerializeField]
        private Sprite betterChipSprite;

        [SerializeField]
        private Sprite englishChipSprite;

        [SerializeField]
        private Sprite goodChipSprite;

        [SerializeField]
        [Tooltip("안내 글 뒤의 칸. 글 높이에 맞춰 늘어난다")]
        private Image bodyTile;

        [SerializeField]
        [Tooltip("요약 탭 안내 칸(따뜻한 색)")]
        private Sprite summaryTileSprite;

        [SerializeField]
        [Tooltip("다른 탭 안내 칸·대화 말풍선(흰 유리)")]
        private Sprite tileSprite;

        [SerializeField]
        private Sprite transcriptAvatarSprite;

        [SerializeField]
        private RectTransform transcriptWordCardTemplate;

        [SerializeField]
        private Sprite transcriptWordCardBackground;

        private const float Px = FeedbackCardsLayout.Px;

        // 요약 카드 → '이 상황에서 자주 써요' 제목 → 표현 줄 사이(시안 px): 카드 아래 18, 제목 높이 25, 제목 아래 10, 줄 사이 10.
        // 문장 교정 카드 사이는 12다.
        private const float GapBelowSummary = 18 * Px;
        private const float SituationTitleHeight = 25 * Px;
        private const float GapBelowSituationTitle = 10 * Px;
        private const float GapBetweenRows = 10 * Px;
        private const float GapBetweenCards = 12 * Px;

        // 안내 글 칸 그림은 그림자 자리(옆 16·위 12·아래 32)를 품고, 그 안쪽에 글 여백(옆 22·위아래 20)을 둔다.
        private const float TileSide = (16 + 22) * Px;
        private const float TileTop = (12 + 20) * Px;
        private const float TileBottom = (32 + 20) * Px;
        private const float PageTitleHeight = 26 * Px;
        private const float GapBelowPageTitle = 14 * Px;
        private const float SubtitleGap = 16 * Px;

        // 판 안은 시안 판(1240×770)을 1.2배로 배치하고, 화면에는 시안 크기로 줄여 보인다. 폭은 시안보다 좁은 1080으로,
        // 마을 HUD를 함께 볼 수 있게 머리줄의 부제(○○와 나눈 대화)와 오른쪽 탭·버튼 사이가 남는 만큼만 줄였다.
        private const float FrameWidth = 1080 * Px;
        private const float FrameHeight = 770 * Px;
        private const float FrameScale = 1 / Px;
        private static readonly Color TabInk = new Color32(47, 37, 27, 255);
        private static readonly Color TabMuted = new Color32(125, 110, 91, 255);

        private ConversationFeedback feedback;
        private IReadOnlyList<DialogueLine> lines;
        private FeedbackSection section;
        private bool isRegenerating;
        private Rect lastFreeArea;
        private bool isTranscriptShown;
        private int? selectedTranscriptReview;
        private FeedbackTranscriptDocument transcriptDocument;
        private TranscriptMeanings meaningSource;
        private int meaningVersion;
        private int meaningLoadingVersion = -1;
        private bool meaningsRequested;
        private bool isMeaningLoading;
        private bool isExpanded;
        private bool isSaveAvailable = true;
        private Func<GlossaryWord, Task<bool>> wordSaver;

        // 고정된 본문 영역 안에서 모든 내용이 세로로 흐른다. 탭을 바꿀 때마다 줄·카드를 새로 만든다.
        private ScrollRect scroll;
        private readonly List<GameObject> builtItems = new List<GameObject>();

        // 표현·문장마다 [담기]. 담은 표현은 다른 탭에 다녀와도 [담음]으로 남는다.
        private readonly Dictionary<string, List<Button>> pickButtons = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly HashSet<string> savedExpressions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>[닫기]를 누르면 알린다.</summary>
        public event Action CloseRequested;

        /// <summary>[담기]로 그 영어 표현·문장과 한국어 뜻을 알린다. 고친 문장은 뜻이 없어 빈 문자열이다.</summary>
        public event Action<string, string> SaveRequested;

        /// <summary>[단어장]을 누르면 알린다.</summary>
        public event Action VocabularyRequested;

        /// <summary>[다시 만들기]를 누르면 알린다.</summary>
        public event Action RegenerateRequested;

        public bool IsVisible => panel.activeSelf;

        /// <summary>피드백 창 전체. 튜토리얼 코치 마크가 첫 대화의 피드백을 밝힌다.</summary>
        public RectTransform PanelRect => (RectTransform)panel.transform;

        /// <summary>[요약]·[문장 교정]·[전체 대화] 탭 줄. 튜토리얼 코치 마크가 피드백 티칭에서 밝힌다.</summary>
        public RectTransform SectionRowRect => sectionRow;

        /// <summary>피드백 본문의 스크롤 영역. 튜토리얼 코치 마크가 피드백 티칭에서 밝힌다.</summary>
        public RectTransform ScrollAreaRect => PageCardRect;

        /// <summary>탭을 누르면 바뀌는 결과 칸. 튜토리얼 코치 마크가 피드백 티칭에서 탭과 함께 밝힌다.</summary>
        public RectTransform PageCardRect =>
            scroll != null
                ? (RectTransform)scroll.transform
                : (RectTransform)bodyText.transform.parent;

        /// <summary>오른쪽 위 X. 튜토리얼 코치 마크가 피드백 티칭 끝에 밝힌다.</summary>
        public RectTransform CloseButtonRect => (RectTransform)topCloseButton.transform;

        public void SetMeaningSource(TranscriptMeanings source) => meaningSource = source;

        public void SetWordSaver(Func<GlossaryWord, Task<bool>> saver) => wordSaver = saver;

        /// <summary>[담기]를 보여 줄지. 저장할 곳이 없으면 끈다.</summary>
        public void SetSaveAvailable(bool available)
        {
            isSaveAvailable = available;
        }

        /// <summary>단어장에 넣은 표현·문장의 [담기]를 [담음]으로 바꾸고 다시 누르지 않게 한다.</summary>
        public void ShowSaved(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;
            savedExpressions.Add(expression.Trim());
            if (!pickButtons.TryGetValue(expression.Trim(), out List<Button> picks))
                return;
            foreach (Button pick in picks)
                MarkPicked(pick);
        }

        /// <summary>Builder와 실행 화면이 같은 고정 스크롤 영역을 쓴다.</summary>
        public void ConfigureScroll()
        {
            var card = (RectTransform)panel.transform.Find("PageCard");
            scroll = card.GetComponent<ScrollRect>();
            if (scroll == null)
                scroll = card.gameObject.AddComponent<ScrollRect>();
            if (card.GetComponent<RectMask2D>() == null)
                card.gameObject.AddComponent<RectMask2D>();
            var hit = card.GetComponent<Image>();
            hit.raycastTarget = true;
            RectTransform content = card.Find("Content") as RectTransform;
            if (content == null)
            {
                content = new GameObject(
                    "Content",
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                content.SetParent(card, false);
                foreach (Transform child in card.Cast<Transform>().ToArray())
                    if (child != content && child.name != "Scrollbar")
                        child.SetParent(content, false);
            }
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-18, card.rect.height);
            scroll.viewport = card;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            Transform scrollbarTransform = card.Find("Scrollbar");
            Scrollbar bar =
                scrollbarTransform != null ? scrollbarTransform.GetComponent<Scrollbar>() : null;
            if (bar == null)
            {
                bar = new GameObject(
                    "Scrollbar",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Scrollbar)
                ).GetComponent<Scrollbar>();
                bar.transform.SetParent(card, false);
                var track = (RectTransform)bar.transform;
                track.anchorMin = new Vector2(1, 0);
                track.anchorMax = Vector2.one;
                track.pivot = new Vector2(1, .5f);
                track.offsetMin = new Vector2(-9, 8);
                track.offsetMax = new Vector2(-1, -8);
                bar.GetComponent<Image>().color = new Color(0, 0, 0, .05f);
                var handle = new GameObject(
                    "Handle",
                    typeof(RectTransform),
                    typeof(Image)
                ).GetComponent<Image>();
                handle.transform.SetParent(bar.transform, false);
                handle.rectTransform.anchorMin = Vector2.zero;
                handle.rectTransform.anchorMax = Vector2.one;
                handle.rectTransform.sizeDelta = Vector2.zero;
                handle.color = new Color(.49f, .43f, .36f, .5f);
                bar.handleRect = handle.rectTransform;
                bar.targetGraphic = handle;
                bar.direction = Scrollbar.Direction.BottomToTop;
            }
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            bodyText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <param name="conversationLines">[대화 기록 보기]로 보여 줄 방금 나눈 대화</param>
        public void Show(
            ConversationFeedback shownFeedback,
            IReadOnlyList<DialogueLine> conversationLines
        )
        {
            isExpanded = false;
            UpdateExpandIcon();
            feedback = shownFeedback;
            lines = conversationLines?.ToList();
            meaningVersion++;
            meaningsRequested = false;
            isMeaningLoading = false;
            isRegenerating = false;
            isTranscriptShown = false;
            selectedTranscriptReview = null;
            savedExpressions.Clear();
            panel.SetActive(true);
            UiLayer.Open(this);
            SelectSection(FeedbackSection.Summary);
        }

        /// <summary>[다시 만들기]를 누른 뒤 새 피드백을 기다리는 동안 보여 준다.</summary>
        public void ShowRegenerating()
        {
            isTranscriptShown = false;
            bodyText.text = "피드백을 다시 만들고 있어요. 잠시만 기다려 주세요.";
            isRegenerating = true;
            bodyText.pageToDisplay = 1;
            navigationRow.gameObject.SetActive(false);
            regenerateButton.interactable = false;
            transcriptButton.interactable = false;
            UpdateLayout();
        }

        public void Hide()
        {
            isExpanded = false;
            meaningVersion++;
            isMeaningLoading = false;
            feedback = null;
            lines = null;
            panel.SetActive(false);
            UiLayer.Close(this);
        }

        private void Awake()
        {
            WindowFrame.Attach((RectTransform)panel.transform);
            ConfigureScroll();
            detailButton.onClick.AddListener(ToggleDetail);
            vocabularyButton.onClick.AddListener(() => VocabularyRequested?.Invoke());
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            topCloseButton.onClick.AddListener(() => CloseRequested?.Invoke());
            expandButton.onClick.AddListener(ToggleExpanded);
            regenerateButton.onClick.AddListener(() => RegenerateRequested?.Invoke());
            transcriptButton.onClick.AddListener(ToggleTranscript);
            TextLinkClicks links = bodyText.GetComponent<TextLinkClicks>();
            if (links == null)
                links = bodyText.gameObject.AddComponent<TextLinkClicks>();
            links.Clicked += SelectTranscriptReview;
            bodyText.raycastTarget = true;
            previousButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            for (int i = 0; i < sectionButtons.Length; i++)
            {
                FeedbackSection selected = (FeedbackSection)i;
                sectionButtons[i].onClick.AddListener(() => SelectSection(selected));
            }
            Hide();
        }

        private void OnEnable() => UiThemeSelection.Shared.Changed += OnThemeChanged;

        private void OnDisable() => UiThemeSelection.Shared.Changed -= OnThemeChanged;

        private void OnThemeChanged(UiThemeId _) => UpdateLayout();

        private void Refresh()
        {
            titleText.text = feedback.IsFailed ? feedback.Title : "대화 피드백";
            summaryText.text = FeedbackText.Partner(feedback.NpcName);
            PlaceSubtitle();
            pageTitleText.text = IsTranscriptView
                ? selectedTranscriptReview.HasValue
                    ? "문장 피드백"
                    : "전체 대화"
                : "이번 대화";
            transcriptDocument = IsTranscriptView
                ? FeedbackTranscript.Build(
                    feedback.NpcName,
                    lines,
                    feedback.Reviews,
                    selectedTranscriptReview
                )
                : null;
            bodyText.text = IsTranscriptView ? "" : MessageText();
            bodyText.pageToDisplay = 1;
            transcriptButtonLabel.text = isTranscriptShown ? "돌아가기" : "대화 기록";
            pageTitleText.gameObject.SetActive(feedback.IsFailed && !IsTranscriptView);
            sectionRow.gameObject.SetActive(!feedback.IsFailed);
            noticeText.gameObject.SetActive(false);
            for (int i = 0; i < sectionButtons.Length; i++)
            {
                sectionButtons[i].interactable = i != (int)section;
                // 고른 탭은 흰 알약 위의 진한 글자, 나머지는 옅은 글자다.
                TMP_Text tab = sectionButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (tab != null)
                    tab.color = i == (int)section ? TabInk : TabMuted;
            }

            // 받지 못한 피드백은 상세 평가가 없으므로 다시 만들기·대화 기록을 보여 준다.
            detailButton.gameObject.SetActive(false);
            regenerateButton.gameObject.SetActive(feedback.CanRegenerate);
            regenerateButton.interactable = true;
            transcriptButton.gameObject.SetActive(feedback.IsFailed);
            transcriptButton.interactable = true;
            // 모든 탭은 같은 본문 영역을 스크롤하므로 이전 쪽 넘김 줄을 숨긴다.
            navigationRow.gameObject.SetActive(false);
            UpdateLayout();
        }

        // 받지 못한 피드백의 이유와 교정할 발화가 없을 때의 안내. 요약 카드·교정 카드를 그리면 비어 있다.
        private string MessageText()
        {
            if (feedback.IsFailed)
                return feedback.Summary;
            if (section == FeedbackSection.Corrections && feedback.Reviews.Count == 0)
                return FeedbackPages.NoCorrections;
            return "";
        }

        private bool IsTranscriptView =>
            isTranscriptShown || section == FeedbackSection.Conversation;

        // 부제(○○와 나눈 대화)는 제목 글자 바로 뒤에 둔다. 제목은 피드백을 받지 못하면 길어진다.
        private void PlaceSubtitle()
        {
            RectTransform title = titleText.rectTransform;
            RectTransform subtitle = summaryText.rectTransform;
            subtitle.anchoredPosition = new Vector2(
                title.anchoredPosition.x
                    + titleText.GetPreferredValues(titleText.text).x
                    + SubtitleGap,
                subtitle.anchoredPosition.y
            );
        }

        // 화면 크기가 바뀌거나 마을 HUD가 나타나고 사라져 빈 곳이 바뀌면 판을 다시 맞춘다.
        private void LateUpdate()
        {
            if (feedback != null && FreeArea() != lastFreeArea)
                UpdateLayout();
        }

        private Rect FreeArea()
        {
            var canvas = (RectTransform)panel.transform.parent;
            if (!isExpanded)
                return CenterWindow.Shared.FreeArea(canvas);
            return new Rect(
                24,
                24,
                Mathf.Max(1, canvas.rect.width - 48),
                Mathf.Max(1, canvas.rect.height - 48)
            );
        }

        /// <summary>현재 탭·선택 문장·스크롤 위치를 유지한 채 창을 넓히거나 원래 크기로 되돌린다.</summary>
        public void ToggleExpanded()
        {
            if (feedback == null)
                return;
            float position = scroll != null ? scroll.verticalNormalizedPosition : 1;
            isExpanded = !isExpanded;
            UpdateLayout();
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.Rebuild(CanvasUpdate.PostLayout);
            scroll.verticalNormalizedPosition = position;
            UpdateExpandIcon();
        }

        // 펼친 창에서는 같은 둥근 버튼에 접기 아이콘을 보인다.
        private void UpdateExpandIcon()
        {
            Image icon = expandButton.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = isExpanded ? collapseSprite : expandSprite;
        }

        // 마을 HUD(퀘스트 카드·미니맵)를 함께 볼 수 있게 그 오른쪽 빈 곳 가운데에 둔다.
        // 빈 곳이 좁거나 낮으면 판을 줄이고 긴 본문은 안에서 스크롤한다.
        private void FitFrameToCanvas()
        {
            var frame = (RectTransform)panel.transform;
            lastFreeArea = FreeArea();
            frame.sizeDelta = new Vector2(
                isExpanded
                    ? lastFreeArea.width / FrameScale
                    : Mathf.Min(FrameWidth, lastFreeArea.width / FrameScale),
                isExpanded
                    ? lastFreeArea.height / FrameScale
                    : Mathf.Min(FrameHeight, lastFreeArea.height / FrameScale)
            );
            // 넓힌 창은 화면을 거의 채우므로 옮기거나 크기를 바꾸지 않는다. 접으면 원래 자리·크기로 돌아온다.
            WindowFrame windowFrame = frame.GetComponent<WindowFrame>();
            if (windowFrame != null)
                windowFrame.enabled = !isExpanded;
            if (isExpanded)
            {
                frame.localScale = Vector3.one * FrameScale;
                frame.anchoredPosition = Vector2.zero;
            }
            else
                CenterWindow.Shared.Place(frame, FrameScale);
        }

        public void UpdateLayout()
        {
            FitFrameToCanvas();
            if (feedback == null || !panel.activeInHierarchy)
                return;
            if (scroll == null)
                ConfigureScroll();
            Canvas.ForceUpdateCanvases();
            ClearBuiltItems();
            navigationRow.gameObject.SetActive(false);
            float width = scroll.content.rect.width;
            bool cards = !IsTranscriptView && !isRegenerating && !feedback.IsFailed;
            summaryCard.gameObject.SetActive(false);
            situationRows.gameObject.SetActive(false);
            float height;
            if (IsTranscriptView && !isRegenerating)
                height = LayoutConversation(width);
            else if (cards && section == FeedbackSection.Summary && !HasMessage)
                height = LayoutSummary(width);
            else if (cards && section == FeedbackSection.Corrections && !HasMessage)
                height = LayoutCorrections(width);
            else
                height = LayoutMessage(width);
            if (!(IsTranscriptView && !isRegenerating))
                bodyText.ForceMeshUpdate();
            scroll.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, height)
            );
        }

        private bool HasMessage => !string.IsNullOrEmpty(bodyText.text);

        // 전체 대화 탭: NPC·내 말풍선, 내 말을 누르면 그 문장의 피드백
        private float LayoutConversation(float width)
        {
            bodyTile.gameObject.SetActive(false);
            GameObject conversation = FeedbackConversationLayout.Build(
                scroll.content,
                bodyText,
                titleText.font,
                sectionButtons[(int)FeedbackSection.Conversation],
                tileSprite,
                transcriptAvatarSprite,
                transcriptWordCardTemplate,
                transcriptWordCardBackground,
                feedback,
                transcriptDocument,
                selectedTranscriptReview,
                SelectReview,
                BackToConversation,
                isMeaningLoading,
                () => _ = LoadTranscriptMeaningsAsync(true),
                wordSaver,
                out float cardsHeight
            );
            builtItems.Add(conversation);
            return cardsHeight;
        }

        // 요약 탭(시안 F1): 요약 카드, 그 아래 '이 상황에서 자주 써요' 제목과 표현 줄
        private float LayoutSummary(float width)
        {
            bodyTile.gameObject.SetActive(false);
            summaryCard.gameObject.SetActive(true);
            float y = FeedbackCardsLayout.SummaryCard(summaryCard, feedback, width);
            PlaceAt(summaryCard, 0);
            IReadOnlyList<SituationExpressionData> situations = FeedbackPages.Situations(feedback);
            if (situations.Count == 0)
                return y;

            situationRows.gameObject.SetActive(true);
            y += GapBelowSummary;
            Pin(situationRows, 0, y, width, 0);
            var heading = (RectTransform)situationRows.Find("Header");
            Pin(heading, 4 * Px, 0, width - 4 * Px, SituationTitleHeight);
            float rowTop = SituationTitleHeight + GapBelowSituationTitle;
            foreach (SituationExpressionData situation in situations)
            {
                GameObject row = Instantiate(situationRowTemplate, situationRows);
                row.name = "SituationRow";
                row.SetActive(true);
                builtItems.Add(row);
                Button pick = row.transform.Find("SaveButton").GetComponent<Button>();
                pick.gameObject.SetActive(isSaveAvailable);
                pick.onClick.AddListener(() =>
                    SaveRequested?.Invoke(situation.Expression, situation.MeaningKo)
                );
                var rect = (RectTransform)row.transform;
                float rowHeight = FeedbackCardsLayout.SituationRow(
                    rect,
                    situation,
                    width,
                    pickSprite
                );
                PlaceAt(rect, rowTop);
                RegisterPick(situation.Expression, pick);
                rowTop += rowHeight + GapBetweenRows;
            }
            float rowsHeight = rowTop - GapBetweenRows;
            situationRows.sizeDelta = new Vector2(width, rowsHeight);
            return y + rowsHeight;
        }

        // 문장 교정 탭(시안 F2): 발화마다 카드를 사이 12로 쌓는다.
        private float LayoutCorrections(float width)
        {
            bodyTile.gameObject.SetActive(false);
            float y = 0;
            foreach (CorrectionCard model in CorrectionCard.From(feedback))
            {
                RectTransform card = Instantiate(correctionCardTemplate, scroll.content);
                card.name = "CorrectionCard";
                card.gameObject.SetActive(true);
                builtItems.Add(card.gameObject);
                Button pick = card.Find("SaveButton").GetComponent<Button>();
                pick.gameObject.SetActive(isSaveAvailable);
                string english = model.Better;
                pick.onClick.AddListener(() => SaveRequested?.Invoke(english, ""));
                float height = FeedbackCardsLayout.Correction(
                    card,
                    model,
                    width,
                    SaidChip(model.Kind),
                    model.Kind == CorrectionKind.Korean ? englishChipSprite : betterChipSprite,
                    pickSprite
                );
                PlaceAt(card, y);
                if (pick.gameObject.activeSelf)
                    RegisterPick(english, pick);
                y += height + GapBetweenCards;
            }
            return Mathf.Max(0, y - GapBetweenCards);
        }

        private Sprite SaidChip(CorrectionKind kind) =>
            kind switch
            {
                CorrectionKind.Corrected => saidChipSprite,
                CorrectionKind.Korean => saidKoreanChipSprite,
                _ => goodChipSprite,
            };

        // 받지 못한 피드백·다시 만드는 중·빈 탭: 장 제목(있으면) 아래 칸에 안내 글
        private float LayoutMessage(float width)
        {
            float top = 0;
            if (pageTitleText.gameObject.activeSelf)
            {
                // 장 제목은 판 아래에 있으므로 본문 칸 위쪽 자리에 맞춘다.
                var card = (RectTransform)scroll.transform;
                RectTransform titleRect = pageTitleText.rectTransform;
                titleRect.anchoredPosition = new Vector2(
                    titleRect.anchoredPosition.x,
                    card.offsetMax.y
                );
                top = PageTitleHeight + GapBelowPageTitle;
            }
            bodyTile.sprite =
                section == FeedbackSection.Summary && !IsTranscriptView
                    ? summaryTileSprite
                    : tileSprite;
            return LayoutBody(bodyText, bodyTile, top, width);
        }

        private float LayoutBody(TMP_Text text, Image tile, float top, float width)
        {
            bool shown = !string.IsNullOrEmpty(text.text);
            tile.gameObject.SetActive(shown);
            if (!shown)
                return top;
            text.overflowMode = TextOverflowModes.Overflow;
            float textWidth = Mathf.Max(1, width - TileSide * 2);
            float height = text.GetPreferredValues(text.text, textWidth, 0).y;
            Pin(text.rectTransform, TileSide, top + TileTop, textWidth, height);
            Pin(tile.rectTransform, 0, top, width, height + TileTop + TileBottom);
            return top + height + TileTop + TileBottom;
        }

        private void RegisterPick(string expression, Button pick)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;
            string key = expression.Trim();
            if (!pickButtons.TryGetValue(key, out List<Button> picks))
            {
                picks = new List<Button>();
                pickButtons[key] = picks;
            }
            picks.Add(pick);
            if (savedExpressions.Contains(key))
                MarkPicked(pick);
        }

        // [담기]를 흰 [담음]으로 바꾼다. 오른쪽 끝과 세로 가운데는 그대로 둔다.
        private void MarkPicked(Button pick)
        {
            if (pick == null)
                return;
            var rect = (RectTransform)pick.transform;
            float right = rect.anchoredPosition.x + rect.sizeDelta.x;
            float middle = -rect.anchoredPosition.y + rect.sizeDelta.y / 2;
            FeedbackCardsLayout.PlacePick(pick, pickedSprite, right, middle);
            pick.interactable = false;
        }

        private void ClearBuiltItems()
        {
            foreach (GameObject item in builtItems)
            {
                if (item == null)
                    continue;
                item.SetActive(false);
                if (Application.isPlaying)
                    Destroy(item);
                else
                    DestroyImmediate(item);
            }
            builtItems.Clear();
            pickButtons.Clear();
        }

        // 콘텐츠 맨 위에서 y만큼 아래에 둔다.
        private static void PlaceAt(RectTransform rect, float y) =>
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);

        // 부모 왼쪽 위 기준 (x, y) 자리에 폭·높이를 둔다.
        private static void Pin(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void SelectSection(FeedbackSection selected)
        {
            section = selected;
            if (section != FeedbackSection.Conversation)
                selectedTranscriptReview = null;
            Refresh();
            scroll.verticalNormalizedPosition = 1;
            if (IsTranscriptView && !meaningsRequested)
                _ = LoadTranscriptMeaningsAsync(false);
        }

        public void PreviousPage() =>
            scroll.verticalNormalizedPosition = Mathf.Clamp01(
                scroll.verticalNormalizedPosition + .75f
            );

        public void NextPage() =>
            scroll.verticalNormalizedPosition = Mathf.Clamp01(
                scroll.verticalNormalizedPosition - .75f
            );

        private void ToggleDetail()
        {
            SelectSection(
                section == FeedbackSection.Summary
                    ? FeedbackSection.Corrections
                    : FeedbackSection.Summary
            );
        }

        private void ToggleTranscript()
        {
            isTranscriptShown = !isTranscriptShown;
            selectedTranscriptReview = null;
            Refresh();
            if (IsTranscriptView && !meaningsRequested)
                _ = LoadTranscriptMeaningsAsync(false);
        }

        private async Task LoadTranscriptMeaningsAsync(bool retry)
        {
            if (meaningSource == null || lines == null || meaningLoadingVersion == meaningVersion)
                return;
            int version = meaningVersion;
            var source = (List<DialogueLine>)lines;
            meaningLoadingVersion = version;
            meaningsRequested = true;
            isMeaningLoading = true;
            Refresh();
            try
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (version != meaningVersion || !IsVisible)
                        return;
                    DialogueLine line = source[i];
                    if (!line.IsNpc || line.Korean != null || line.TurnId <= 0)
                        continue;
                    try
                    {
                        DialogueLine resolved = await meaningSource.ResolveAsync(line, retry);
                        if (this == null || version != meaningVersion || !IsVisible)
                            return;
                        source[i] = resolved;
                        RefreshTranscriptKeepingPosition();
                    }
                    catch (ApiException)
                    {
                        // 원문은 계속 읽을 수 있다. 사용자가 다시 불러오기를 눌렀을 때만 재시도한다.
                    }
                }
            }
            finally
            {
                if (this != null && version == meaningVersion)
                {
                    isMeaningLoading = false;
                    meaningLoadingVersion = -1;
                    if (IsVisible)
                        RefreshTranscriptKeepingPosition();
                }
            }
        }

        private void RefreshTranscriptKeepingPosition()
        {
            float position = scroll.verticalNormalizedPosition;
            Refresh();
            scroll.verticalNormalizedPosition = position;
        }

        private void SelectTranscriptReview(string linkId)
        {
            const string prefix = "review:";
            if (
                !IsTranscriptView
                || string.IsNullOrEmpty(linkId)
                || !linkId.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(linkId.Substring(prefix.Length), out int reviewIndex)
            )
                return;

            SelectReview(reviewIndex);
        }

        /// <summary>전체 대화의 사용자 발화를 선택해 해당 문장의 기존 평가를 펼친다.</summary>
        public void SelectReview(int reviewIndex)
        {
            if (
                !IsTranscriptView
                || feedback == null
                || reviewIndex < 0
                || reviewIndex >= feedback.Reviews.Count
            )
                return;

            selectedTranscriptReview = reviewIndex;
            Refresh();
            scroll.verticalNormalizedPosition = 1;
        }

        private void BackToConversation()
        {
            selectedTranscriptReview = null;
            Refresh();
            scroll.verticalNormalizedPosition = 1;
        }
    }
}
