using System;
using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤의 피드백을 주제별로 나누고 문장·표현을 고정 영역 안에서 세로 스크롤로 보여 준다.
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
        private TMP_Text bodyText;

        [SerializeField]
        private Button saveButton;

        [SerializeField]
        private Button detailButton;

        [SerializeField]
        private TMP_Text detailButtonLabel;

        [SerializeField]
        private Button vocabularyButton;

        [SerializeField]
        private Button reviewButton;
        public event Action ReviewSaveRequested;

        public void SetReviewAvailable(bool available)
        {
            if (reviewButton != null)
                reviewButton.gameObject.SetActive(available);
        }

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button topCloseButton;

        [SerializeField]
        private Button regenerateButton;

        [SerializeField]
        private Button transcriptButton;

        [SerializeField]
        private TMP_Text transcriptButtonLabel;

        private ConversationFeedback feedback;
        private IReadOnlyList<DialogueLine> lines;

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
        [Tooltip("요약·회화 표현 탭의 '이 상황에서 자주 써요' 표현 줄 묶음")]
        private RectTransform situationRows;

        [SerializeField]
        [Tooltip("표현 줄 원본. 숨겨 두고 표현마다 복사해 쓴다")]
        private GameObject situationRowTemplate;

        [SerializeField]
        [Tooltip("표현 줄 아래 '대화 요약' 제목. 요약 탭에서 요약 글이 있을 때 보인다")]
        private TMP_Text summaryTitle;

        [SerializeField]
        [Tooltip("이전 프리팹의 표현 쪽 넘김 참조. 스크롤 화면에서는 숨긴다")]
        private RectTransform situationPager;

        [SerializeField]
        [Tooltip("이전 프리팹의 요약 쪽 넘김 참조. 스크롤 화면에서는 숨긴다")]
        private RectTransform summaryPager;

        [SerializeField]
        [Tooltip("본문 글 뒤의 칸. 글 높이에 맞춰 늘어난다")]
        private Image bodyTile;

        [SerializeField]
        [Tooltip("요약 탭 본문 칸(따뜻한 색)")]
        private Sprite summaryTileSprite;

        [SerializeField]
        [Tooltip("다른 탭 본문 칸(흰 유리)")]
        private Sprite tileSprite;

        [SerializeField]
        private Sprite transcriptAvatarSprite;

        // 표현 칸 마지막 칸 아래 → 제목 → 본문 칸 사이 간격(시안 px × 1.2). 표현 칸이 없을 때는 카드 맨 위에서 시작한다.
        // 칸 그림은 아래 32·위 12의 그림자 자리를 품고 있어, 보이는 간격이 시안(20, 14)이 되게 그만큼 뺀다.
        private const float RowShadowBottom = 32 * 1.2f;
        private const float TileShadowTop = 12 * 1.2f;
        private const float GapBelowRows = 20 * 1.2f;
        private const float GapBelowHeading = 14 * 1.2f;
        private const float BodyTopPadding = 0;
        private const float SummaryTitleHeight = 26 * 1.2f;
        private const float SubtitleGap = 16 * 1.2f;

        // 판 안은 시안 판(1240×770)을 1.2배로 배치하고, 화면에는 시안 크기로 줄여 보인다. 폭은 시안보다 좁은 1080으로,
        // 머리줄의 부제(○○와 나눈 대화)와 오른쪽 탭 사이가 남는 만큼만 줄였다.
        private const float FrameWidth = 1080 * 1.2f;
        private const float FrameHeight = 770 * 1.2f;
        private const float FrameScale = 1 / 1.2f;
        private static readonly Color TabInk = new Color32(47, 37, 27, 255);
        private static readonly Color TabMuted = new Color32(125, 110, 91, 255);

        // 본문 칸 그림은 그림자 자리(옆 16·위 12·아래 32)를 품고, 그 안쪽에 글 여백(옆 22·위아래 20)을 둔다.
        private const float TileSide = (16 + 22) * 1.2f;
        private const float TileTop = (12 + 20) * 1.2f;
        private const float TileBottom = (32 + 20) * 1.2f;

        private readonly List<GameObject> situationRowItems = new List<GameObject>();

        // 표현마다 [담기] 버튼. 저장하면 "담음"으로 바꾼다.
        private readonly Dictionary<string, Button> saveButtons = new Dictionary<string, Button>(
            StringComparer.OrdinalIgnoreCase
        );
        private IReadOnlyList<FeedbackPage> pages;
        private FeedbackSection section;
        private bool isRegenerating;
        private Rect lastFreeArea;
        private bool isTranscriptShown;
        private int? selectedTranscriptReview;
        private FeedbackTranscriptDocument transcriptDocument;

        // 고정된 본문 영역 안에서 모든 내용이 세로로 흐른다.
        private ScrollRect scroll;
        private readonly List<GameObject> additionalBodies = new List<GameObject>();

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

        private bool isSaveAvailable = true;

        /// <summary>[닫기]를 누르면 알린다.</summary>
        public event Action CloseRequested;

        /// <summary>[표현 저장]으로 현재 카드의 영어 문장과 한국어 뜻을 알린다.</summary>
        public event Action<string, string> SaveRequested;

        /// <summary>[단어장 보기]를 누르면 알린다.</summary>
        public event Action VocabularyRequested;

        /// <summary>[다시 만들기]를 누르면 알린다.</summary>
        public event Action RegenerateRequested;

        public bool IsVisible => panel.activeSelf;

        /// <summary>피드백 창 전체. 튜토리얼 코치 마크가 첫 대화의 피드백을 밝힌다.</summary>
        public RectTransform PanelRect => (RectTransform)panel.transform;

        /// <summary>[요약]·[문장]·[회화]·[표현] 탭 줄. 튜토리얼 코치 마크가 피드백 티칭에서 밝힌다.</summary>
        public RectTransform SectionRowRect => sectionRow;

        /// <summary>피드백 본문의 스크롤 영역. 튜토리얼 코치 마크가 피드백 티칭에서 밝힌다.</summary>
        public RectTransform ScrollAreaRect => PageCardRect;

        /// <summary>탭·화살표를 누르면 바뀌는 결과 칸. 튜토리얼 코치 마크가 피드백 티칭에서 탭과 함께 밝힌다.</summary>
        public RectTransform PageCardRect =>
            scroll != null
                ? (RectTransform)scroll.transform
                : (RectTransform)bodyText.transform.parent;

        /// <summary>오른쪽 위 X. 튜토리얼 코치 마크가 피드백 티칭 끝에 밝힌다.</summary>
        public RectTransform CloseButtonRect => (RectTransform)topCloseButton.transform;

        /// <summary>[표현 저장]을 보여 줄지. 저장할 곳이 없으면 끈다.</summary>
        public void SetSaveAvailable(bool available)
        {
            isSaveAvailable = available;
        }

        /// <summary>단어장에 넣은 표현의 [담기]를 "담음"으로 바꾸고 다시 누르지 않게 한다.</summary>
        public void ShowSaved(string expression)
        {
            if (!saveButtons.TryGetValue(expression, out Button save))
                return;
            save.interactable = false;
            TMP_Text label = save.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "담음";
        }

        /// <param name="conversationLines">[대화 기록 보기]로 보여 줄 방금 나눈 대화</param>
        public void Show(
            ConversationFeedback shownFeedback,
            IReadOnlyList<DialogueLine> conversationLines
        )
        {
            feedback = shownFeedback;
            lines = conversationLines;
            isRegenerating = false;
            isTranscriptShown = false;
            selectedTranscriptReview = null;
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
            ShowSituationRows(Array.Empty<SituationExpressionData>(), false);
            UpdateLayout();
        }

        public void Hide()
        {
            feedback = null;
            lines = null;
            panel.SetActive(false);
            UiLayer.Close(this);
        }

        private void Awake()
        {
            ConfigureScroll();
            saveButton.onClick.AddListener(RequestSave);
            detailButton.onClick.AddListener(ToggleDetail);
            vocabularyButton.onClick.AddListener(() => VocabularyRequested?.Invoke());
            if (reviewButton != null)
                reviewButton.onClick.AddListener(() => ReviewSaveRequested?.Invoke());
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            topCloseButton.onClick.AddListener(() => CloseRequested?.Invoke());
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
                : pages[0].Title;
            transcriptDocument = IsTranscriptView
                ? FeedbackTranscript.Build(
                    feedback.NpcName,
                    lines,
                    feedback.Reviews,
                    selectedTranscriptReview
                )
                : null;
            bodyText.text = IsTranscriptView ? "" : pages[0].Body;
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
                {
                    if (i == (int)FeedbackSection.Conversation)
                        tab.text = "전체 대화";
                    tab.color = i == (int)section ? TabInk : TabMuted;
                }
            }

            // 받지 못한 피드백은 저장할 표현이나 상세 평가가 없으므로 그 버튼 대신 다시 만들기·대화 기록을 보여 준다.
            // 본문 옆 [담기]는 지금 장에 담을 표현이 있을 때만 둔다. 추천 표현 줄은 각자 버튼을 가진다.
            saveButton.gameObject.SetActive(
                isSaveAvailable
                    && !feedback.IsFailed
                    && !IsTranscriptView
                    && CurrentExpression != null
                    && pages[0].Situations == null
            );
            saveButton.interactable = CurrentExpression != null;
            saveButton.GetComponentInChildren<TMP_Text>(true).text = "담기";
            detailButton.gameObject.SetActive(false);
            regenerateButton.gameObject.SetActive(feedback.CanRegenerate);
            regenerateButton.interactable = true;
            transcriptButton.gameObject.SetActive(feedback.IsFailed);
            transcriptButton.interactable = true;
            // 모든 탭은 같은 본문 영역을 스크롤하므로 이전 쪽 넘김 줄을 숨긴다.
            navigationRow.gameObject.SetActive(false);
            // 표현 줄 높이는 패널 너비에 따라 달라지므로 화면에 맞춘 패널 크기부터 정한다.
            FitFrameToCanvas();
            ShowSituationRows(SituationsShown(), false);
            UpdateLayout();
        }

        // 상황 표현은 요약 탭에 모아 두고, 전체 대화 탭은 주고받은 문장에 집중한다.
        private IReadOnlyList<SituationExpressionData> SituationsShown()
        {
            if (feedback.IsFailed || IsTranscriptView)
                return Array.Empty<SituationExpressionData>();
            return section == FeedbackSection.Summary
                ? SummarySituations.ToList()
                : pages
                    .Where(page => page.Situations != null)
                    .SelectMany(page => page.Situations)
                    .ToList();
        }

        // 요약 탭이 위 표현 칸과 아래 요약 칸으로 나뉘는 상태인지
        private bool IsTranscriptView =>
            isTranscriptShown || section == FeedbackSection.Conversation;

        private bool IsSummarySplit =>
            section == FeedbackSection.Summary && !feedback.IsFailed && !IsTranscriptView;

        private IEnumerable<SituationExpressionData> SummarySituations =>
            feedback.SituationExpressions.Where(expression =>
                !string.IsNullOrWhiteSpace(expression.Expression)
            );

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

        /// <summary>
        /// 피드백을 열자마자 보이도록 요약 탭 위쪽에 이 상황에서 자주 쓰는 표현을 모두 보이고, 칸마다 [담기]로 그 표현과 뜻을 담게 한다.
        /// 회화 표현 탭도 같은 칸을 쓰고 예문을 한 줄 더 둔다. 요약 글은 표현 칸 아래 '대화 요약' 제목 밑의 따뜻한 색 칸에서 시작한다.
        /// </summary>
        private void ShowSituationRows(
            IReadOnlyList<SituationExpressionData> expressions,
            bool withExamples
        )
        {
            foreach (GameObject row in situationRowItems)
            {
                row.SetActive(false);
                if (Application.isPlaying)
                    Destroy(row);
                else
                    DestroyImmediate(row);
            }
            situationRowItems.Clear();
            saveButtons.Clear();
            if (saveButton.gameObject.activeSelf && CurrentExpression != null)
                saveButtons[CurrentExpression.English] = saveButton;

            foreach (SituationExpressionData expression in expressions)
            {
                if (string.IsNullOrWhiteSpace(expression.Expression))
                    continue;
                GameObject row = Instantiate(situationRowTemplate, situationRows);
                row.name = "SituationRow";
                SetRowText(row, "Texts/Line/Phrase", expression.Expression);
                SetRowText(row, "Texts/Line/Meaning", expression.MeaningKo);
                SetRowText(row, "Texts/Usage", expression.UsageKo);
                SetRowText(
                    row,
                    "Texts/Example",
                    withExamples && !string.IsNullOrEmpty(expression.ExampleEn)
                        ? string.IsNullOrEmpty(expression.ExampleKo)
                            ? $"예) {expression.ExampleEn}"
                            : $"예) {expression.ExampleEn} · {expression.ExampleKo}"
                        : ""
                );
                Button save = row.GetComponentInChildren<Button>(true);
                save.gameObject.SetActive(isSaveAvailable);
                save.onClick.AddListener(() =>
                    SaveRequested?.Invoke(expression.Expression, expression.MeaningKo)
                );
                saveButtons[expression.Expression] = save;
                row.SetActive(true);
                situationRowItems.Add(row);
            }

            bool visible = situationRowItems.Count > 0;
            situationRows.gameObject.SetActive(visible);
            float top = BodyTopPadding;
            if (visible)
            {
                // 카드 크기는 패널이 정한다. 방금 켠 패널은 배치가 아직 돌지 않아 원래 크기라서, 줄 너비를 재기 전에 먼저 배치한다.
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(situationRows);
                foreach (GameObject row in situationRowItems)
                    ArrangeFirstLine((RectTransform)row.transform.Find("Texts/Line"));
                LayoutRebuilder.ForceRebuildLayoutImmediate(situationRows);
                top =
                    -situationRows.anchoredPosition.y
                    + situationRows.rect.height
                    - RowShadowBottom
                    + GapBelowRows;
            }
            // 표현 칸이 없어도 요약 글이 있으면 '대화 요약' 제목을 둔다.
            bool summaryHeading = IsSummarySplit && !string.IsNullOrEmpty(bodyText.text);
            summaryTitle.gameObject.SetActive(summaryHeading);
            // 받지 못한 피드백·대화 기록의 장 제목도 같은 자리(본문 칸 위)에 둔다.
            TMP_Text heading =
                summaryHeading ? summaryTitle
                : pageTitleText.gameObject.activeSelf ? pageTitleText
                : null;
            if (heading != null)
            {
                // 장 제목은 판 아래에 있으므로 본문 칸 위쪽 자리만큼 더 내린다.
                var card = (RectTransform)bodyText.rectTransform.parent;
                float cardTop = heading.transform.parent == card ? 0 : card.offsetMax.y;
                RectTransform titleRect = heading.rectTransform;
                titleRect.anchoredPosition = new Vector2(
                    titleRect.anchoredPosition.x,
                    cardTop - top
                );
                top += SummaryTitleHeight + GapBelowHeading - TileShadowTop;
            }
            RectTransform body = bodyText.rectTransform;
            var saveRect = (RectTransform)saveButton.transform;
            float saveRoom = saveButton.gameObject.activeSelf ? saveRect.sizeDelta.x + 24 : 0;
            body.offsetMax = new Vector2(-TileSide - saveRoom, -(top + TileTop));
            PlaceBodyTile(top);
        }

        // 본문 글 뒤의 칸을 글 높이에 맞춘다. 요약 탭은 따뜻한 색, 다른 탭은 흰 유리 칸이고, 글이 없으면 숨긴다.
        private void PlaceBodyTile(float top)
        {
            bool shown = !string.IsNullOrEmpty(bodyText.text);
            bodyTile.gameObject.SetActive(shown);
            if (!shown)
                return;
            bodyTile.sprite =
                section == FeedbackSection.Summary && !IsTranscriptView
                    ? summaryTileSprite
                    : tileSprite;
            RectTransform body = bodyText.rectTransform;
            float contentHeight = Mathf.Min(
                bodyText.GetPreferredValues(bodyText.text, body.rect.width, 0).y,
                body.rect.height
            );
            var saveRect = (RectTransform)saveButton.transform;
            if (saveButton.gameObject.activeSelf)
                contentHeight = Mathf.Max(contentHeight, saveRect.sizeDelta.y);
            float height = contentHeight + TileTop + TileBottom;
            RectTransform tile = bodyTile.rectTransform;
            tile.anchorMin = new Vector2(0, 1);
            tile.anchorMax = Vector2.one;
            tile.pivot = new Vector2(.5f, 1);
            tile.offsetMin = new Vector2(body.offsetMin.x - TileSide, -(top + height));
            tile.offsetMax = new Vector2(0, -top);
            saveRect.anchoredPosition = new Vector2(-TileSide, -TileTop);
        }

        private static void SetRowText(GameObject row, string path, string value)
        {
            TMP_Text text = row.transform.Find(path).GetComponent<TMP_Text>();
            text.text = value ?? "";
            text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        // 표현 칸 첫 줄: 영어 표현 뒤에 자리가 있으면 한국어 뜻을 같은 줄에, 모자라면 다음 줄에 둔다.
        private static void ArrangeFirstLine(RectTransform line)
        {
            var phrase = line.Find("Phrase").GetComponent<TMP_Text>();
            var meaning = line.Find("Meaning").GetComponent<TMP_Text>();
            float width = line.rect.width;
            Vector2 phraseSize = phrase.GetPreferredValues(phrase.text, width, 0);
            float phraseWidth = Mathf.Min(phraseSize.x, width);
            Vector2 meaningSize = meaning.GetPreferredValues(meaning.text, width, 0);
            bool sameLine = SituationRowLayout.MeaningOnSameLine(phraseWidth, meaningSize.x, width);
            float phraseHeight = phrase.GetPreferredValues(phrase.text, phraseWidth, 0).y;
            Pin(phrase.rectTransform, 0, 0, sameLine ? phraseWidth : width, phraseHeight);
            if (sameLine)
                Pin(
                    meaning.rectTransform,
                    phraseWidth + SituationRowLayout.Gap,
                    (phraseHeight - meaningSize.y) / 2,
                    meaningSize.x,
                    meaningSize.y
                );
            else
                Pin(meaning.rectTransform, 0, phraseHeight, width, meaningSize.y);
            line.GetComponent<LayoutElement>().preferredHeight = sameLine
                ? Mathf.Max(phraseHeight, meaningSize.y)
                : phraseHeight + meaningSize.y;
        }

        // 줄 왼쪽 위 기준 (x, y) 자리에 폭·높이를 둔다.
        private static void Pin(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private ReferenceExpression CurrentExpression =>
            pages != null && pages.Count > 0 ? pages[0].Expression : null;

        // 화면 크기가 바뀌거나 마을 HUD가 나타나고 사라져 빈 곳이 바뀌면 판을 다시 맞춘다.
        private void LateUpdate()
        {
            if (feedback != null && FreeArea() != lastFreeArea)
                UpdateLayout();
        }

        private Rect FreeArea()
        {
            return CenterWindow.Shared.FreeArea((RectTransform)panel.transform.parent);
        }

        // 마을 HUD(퀘스트 카드·미니맵)를 함께 볼 수 있게 그 오른쪽 빈 곳 가운데에 둔다.
        // 빈 곳이 좁거나 낮으면 판을 줄이고 긴 본문은 안에서 스크롤한다.
        private void FitFrameToCanvas()
        {
            var frame = (RectTransform)panel.transform;
            lastFreeArea = FreeArea();
            frame.sizeDelta = new Vector2(
                Mathf.Min(FrameWidth, lastFreeArea.width / FrameScale),
                Mathf.Min(FrameHeight, lastFreeArea.height / FrameScale)
            );
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
            foreach (GameObject block in additionalBodies)
            {
                block.SetActive(false);
                if (Application.isPlaying)
                    Destroy(block);
                else
                    DestroyImmediate(block);
            }
            additionalBodies.Clear();
            navigationRow.gameObject.SetActive(false);
            if (situationPager != null)
            {
                situationPager.gameObject.SetActive(false);
            }
            if (summaryPager != null)
            {
                summaryPager.gameObject.SetActive(false);
            }
            float width = scroll.content.rect.width;
            if (IsTranscriptView && !isRegenerating)
            {
                bodyTile.gameObject.SetActive(false);
                GameObject cards = FeedbackConversationLayout.Build(
                    scroll.content,
                    bodyText,
                    titleText.font,
                    sectionButtons[(int)FeedbackSection.Conversation],
                    tileSprite,
                    transcriptAvatarSprite,
                    feedback,
                    transcriptDocument,
                    selectedTranscriptReview,
                    SelectReview,
                    BackToConversation,
                    out float cardsHeight
                );
                additionalBodies.Add(cards);
                scroll.content.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(scroll.viewport.rect.height, cardsHeight)
                );
                return;
            }
            // 줄바꿈이 끝난 표현 칸 아래부터 제목과 본문을 차례로 쌓는다.
            if (situationRows.gameObject.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(situationRows);
                foreach (GameObject row in situationRowItems)
                    ArrangeFirstLine((RectTransform)row.transform.Find("Texts/Line"));
                LayoutRebuilder.ForceRebuildLayoutImmediate(situationRows);
            }
            float top = situationRows.gameObject.activeSelf
                ? situationRows.rect.height - RowShadowBottom + GapBelowRows
                : 0;
            bool summaryHeading = IsSummarySplit && !string.IsNullOrEmpty(bodyText.text);
            summaryTitle.gameObject.SetActive(summaryHeading);
            if (summaryHeading)
            {
                Pin(summaryTitle.rectTransform, 0, top, width, SummaryTitleHeight);
                top += SummaryTitleHeight + GapBelowHeading;
            }
            top = LayoutBody(bodyText, bodyTile, saveButton, top, width);
            if (!IsTranscriptView && !isRegenerating)
            {
                foreach (FeedbackPage page in pages.Skip(1))
                {
                    if (string.IsNullOrEmpty(page.Body))
                        continue;
                    Image tile = Instantiate(bodyTile, scroll.content);
                    TMP_Text text = Instantiate(bodyText, scroll.content);
                    Button save = tile.GetComponentInChildren<Button>(true);
                    additionalBodies.Add(tile.gameObject);
                    additionalBodies.Add(text.gameObject);
                    text.text = page.Body;
                    if (save != null)
                    {
                        save.onClick.RemoveAllListeners();
                        ReferenceExpression expression = page.Expression;
                        save.gameObject.SetActive(isSaveAvailable && expression != null);
                        save.interactable = true;
                        if (expression != null)
                        {
                            save.onClick.AddListener(() =>
                                SaveRequested?.Invoke(expression.English, expression.Korean)
                            );
                            saveButtons[expression.English] = save;
                        }
                    }
                    top = LayoutBody(text, tile, save, top, width);
                }
            }
            scroll.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, top)
            );
            bodyText.ForceMeshUpdate();
        }

        private float LayoutBody(TMP_Text text, Image tile, Button save, float top, float width)
        {
            bool shown = !string.IsNullOrEmpty(text.text);
            tile.gameObject.SetActive(shown);
            if (!shown)
                return top;
            text.overflowMode = TextOverflowModes.Overflow;
            float saveRoom =
                save != null && save.gameObject.activeSelf
                    ? ((RectTransform)save.transform).sizeDelta.x + 24
                    : 0;
            float textWidth = Mathf.Max(1, width - TileSide * 2 - saveRoom);
            float height = text.GetPreferredValues(text.text, textWidth, 0).y;
            if (save != null && save.gameObject.activeSelf)
                height = Mathf.Max(height, ((RectTransform)save.transform).sizeDelta.y);
            Pin(text.rectTransform, TileSide, top + TileTop, textWidth, height);
            Pin(tile.rectTransform, 0, top, width, height + TileTop + TileBottom);
            if (save != null)
                ((RectTransform)save.transform).anchoredPosition = new Vector2(-TileSide, -TileTop);
            return top + height + TileTop + TileBottom + GapBelowRows;
        }

        public void SelectSection(FeedbackSection selected)
        {
            section = selected;
            if (section != FeedbackSection.Conversation)
                selectedTranscriptReview = null;
            pages = FeedbackPages.Create(feedback, section);
            Refresh();
            scroll.verticalNormalizedPosition = 1;
        }

        public void PreviousPage() =>
            scroll.verticalNormalizedPosition = Mathf.Clamp01(
                scroll.verticalNormalizedPosition + .75f
            );

        public void NextPage() =>
            scroll.verticalNormalizedPosition = Mathf.Clamp01(
                scroll.verticalNormalizedPosition - .75f
            );

        private void RequestSave()
        {
            ReferenceExpression expression = CurrentExpression;
            if (expression != null)
            {
                SaveRequested?.Invoke(expression.English, expression.Korean);
            }
        }

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
