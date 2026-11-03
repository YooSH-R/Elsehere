using System;
using System.Collections.Generic;
using A305.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤의 피드백을 주제별로 나누고 문장·표현을 한 장씩 보여 준다.
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
        [Tooltip("요약 탭 위쪽의 '이 상황에서 자주 써요' 표현 줄 묶음")]
        private RectTransform situationRows;

        [SerializeField]
        [Tooltip("표현 줄 원본. 숨겨 두고 표현마다 복사해 쓴다")]
        private GameObject situationRowTemplate;

        [SerializeField]
        [Tooltip("표현 줄 아래 '대화 요약' 제목. 요약 탭에서 표현 줄이 있을 때만 보인다")]
        private TMP_Text summaryTitle;

        [SerializeField]
        [Tooltip("본문 글 뒤의 칸. 글 높이에 맞춰 늘어난다")]
        private Image bodyTile;

        [SerializeField]
        [Tooltip("요약 탭 본문 칸(따뜻한 색)")]
        private Sprite summaryTileSprite;

        [SerializeField]
        [Tooltip("다른 탭 본문 칸(흰 유리)")]
        private Sprite tileSprite;

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
        // 위아래에는 화면 위 가운데의 튜토리얼 [건너뛰기] 카드(위에서 24, 높이 약 110)가 들어갈 자리를 남긴다.
        private const float FrameWidth = 1080 * 1.2f;
        private const float FrameHeight = 770 * 1.2f;
        private const float FrameScale = 1 / 1.2f;
        private const float FrameClearance = 150;
        private const float FrameSideMargin = 24;
        private static readonly Color TabInk = new Color32(47, 37, 27, 255);
        private static readonly Color TabMuted = new Color32(125, 110, 91, 255);

        // 본문 칸 그림은 그림자 자리(옆 16·위 12·아래 32)를 품고, 그 안쪽에 글 여백(옆 22·위아래 20)을 둔다.
        private const float TileSide = (16 + 22) * 1.2f;
        private const float TileTop = (12 + 20) * 1.2f;
        private const float TileBottom = (32 + 20) * 1.2f;

        private readonly List<GameObject> situationRowItems = new List<GameObject>();
        private IReadOnlyList<FeedbackPage> pages;
        private FeedbackSection section;
        private int pageIndex;
        private int textPage = 1;
        private bool isRegenerating;
        private Vector2 lastCanvasSize;
        private bool isTranscriptShown;
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

        /// <summary>쪽 넘김 화살표 줄. 튜토리얼 코치 마크가 피드백 티칭에서 밝힌다.</summary>
        public RectTransform NavigationRowRect => navigationRow;

        /// <summary>탭·화살표를 누르면 바뀌는 결과 칸. 튜토리얼 코치 마크가 피드백 티칭에서 탭과 함께 밝힌다.</summary>
        public RectTransform PageCardRect => (RectTransform)bodyText.transform.parent;

        /// <summary>오른쪽 위 X. 튜토리얼 코치 마크가 피드백 티칭 끝에 밝힌다.</summary>
        public RectTransform CloseButtonRect => (RectTransform)topCloseButton.transform;

        /// <summary>[표현 저장]을 보여 줄지. 저장할 곳이 없으면 끈다.</summary>
        public void SetSaveAvailable(bool available)
        {
            isSaveAvailable = available;
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
            panel.SetActive(true);
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
        }

        public void Hide()
        {
            feedback = null;
            lines = null;
            panel.SetActive(false);
        }

        private void Awake()
        {
            saveButton.onClick.AddListener(RequestSave);
            detailButton.onClick.AddListener(ToggleDetail);
            vocabularyButton.onClick.AddListener(() => VocabularyRequested?.Invoke());
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            topCloseButton.onClick.AddListener(() => CloseRequested?.Invoke());
            regenerateButton.onClick.AddListener(() => RegenerateRequested?.Invoke());
            transcriptButton.onClick.AddListener(ToggleTranscript);
            previousButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            for (int i = 0; i < sectionButtons.Length; i++)
            {
                FeedbackSection selected = (FeedbackSection)i;
                sectionButtons[i].onClick.AddListener(() => SelectSection(selected));
            }
            Hide();
        }

        private void Refresh()
        {
            titleText.text = feedback.IsFailed ? feedback.Title : "대화 피드백";
            summaryText.text = FeedbackText.Partner(feedback.NpcName);
            PlaceSubtitle();
            pageTitleText.text = isTranscriptShown ? "주고받은 대화" : pages[pageIndex].Title;
            bodyText.text = isTranscriptShown
                ? FeedbackText.Transcript(feedback.NpcName, lines)
                : pages[pageIndex].Body;
            bodyText.pageToDisplay = textPage;
            transcriptButtonLabel.text = isTranscriptShown ? "돌아가기" : "대화 기록";
            pageTitleText.gameObject.SetActive(feedback.IsFailed || isTranscriptShown);
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

            // 받지 못한 피드백은 저장할 표현이나 상세 평가가 없으므로 그 버튼 대신 다시 만들기·대화 기록을 보여 준다.
            // 아래 줄 [담기]는 지금 장에 담을 표현이 있을 때만 둔다. 표현 칸은 칸마다 [담기]가 있다.
            saveButton.gameObject.SetActive(
                isSaveAvailable && !feedback.IsFailed && CurrentExpression != null
            );
            saveButton.interactable = CurrentExpression != null;
            detailButton.gameObject.SetActive(false);
            regenerateButton.gameObject.SetActive(feedback.CanRegenerate);
            regenerateButton.interactable = true;
            transcriptButton.gameObject.SetActive(feedback.IsFailed);
            transcriptButton.interactable = true;
            navigationRow.gameObject.SetActive(
                !isRegenerating && (!feedback.IsFailed || isTranscriptShown)
            );
            // 표현 줄 높이는 패널 너비에 따라 달라지므로 화면에 맞춘 패널 크기부터 정한다.
            FitFrameToCanvas();
            ShowSituationRows(SituationsShown(), section == FeedbackSection.Conversation);
            UpdateLayout();
        }

        // 요약 탭은 받은 피드백의 표현을, 회화 표현 탭은 그 장에 담긴 표현을 같은 표현 줄로 보인다.
        private IReadOnlyList<SituationExpressionData> SituationsShown()
        {
            if (feedback.IsFailed || isTranscriptShown)
                return Array.Empty<SituationExpressionData>();
            if (section == FeedbackSection.Summary)
                return feedback.SituationExpressions;
            return pages[pageIndex].Situations ?? Array.Empty<SituationExpressionData>();
        }

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
        /// 피드백을 열자마자 보이도록 요약 탭 위쪽에 이 상황에서 자주 쓰는 표현을 한 칸씩 모두 보이고, 칸마다 [담기]로 그 표현과 뜻을 담게 한다.
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
            bool summaryBelowRows =
                visible
                && section == FeedbackSection.Summary
                && !string.IsNullOrEmpty(bodyText.text);
            summaryTitle.gameObject.SetActive(summaryBelowRows);
            // 받지 못한 피드백·대화 기록의 장 제목도 같은 자리(본문 칸 위)에 둔다.
            TMP_Text heading =
                summaryBelowRows ? summaryTitle
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
            body.offsetMax = new Vector2(body.offsetMax.x, -(top + TileTop));
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
                section == FeedbackSection.Summary && !isTranscriptShown
                    ? summaryTileSprite
                    : tileSprite;
            RectTransform body = bodyText.rectTransform;
            float height =
                Mathf.Min(
                    bodyText.GetPreferredValues(bodyText.text, body.rect.width, 0).y,
                    body.rect.height
                )
                + TileTop
                + TileBottom;
            RectTransform tile = bodyTile.rectTransform;
            tile.anchorMin = new Vector2(0, 1);
            tile.anchorMax = Vector2.one;
            tile.pivot = new Vector2(.5f, 1);
            tile.offsetMin = new Vector2(body.offsetMin.x - TileSide, -(top + height));
            tile.offsetMax = new Vector2(body.offsetMax.x + TileSide, -top);
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
            pages != null && pages.Count > 0 ? pages[pageIndex].Expression : null;

        private void LateUpdate()
        {
            if (
                feedback != null
                && ((RectTransform)panel.transform.parent).rect.size != lastCanvasSize
            )
                UpdateLayout();
        }

        private void FitFrameToCanvas()
        {
            var frame = (RectTransform)panel.transform;
            var canvas = (RectTransform)frame.parent;
            lastCanvasSize = canvas.rect.size;
            // 작은 화면에서는 배치하는 판을 줄여(본문은 쪽을 더 나눈다) 양옆 24와 위아래 카드 자리를 남긴다.
            frame.localScale = Vector3.one * FrameScale;
            frame.sizeDelta = new Vector2(
                Mathf.Min(FrameWidth, (canvas.rect.width - 2 * FrameSideMargin) / FrameScale),
                Mathf.Min(FrameHeight, (canvas.rect.height - 2 * FrameClearance) / FrameScale)
            );
        }

        public void UpdateLayout()
        {
            FitFrameToCanvas();
            if (feedback == null || !panel.activeInHierarchy)
                return;
            Canvas.ForceUpdateCanvases();
            int count = isTranscriptShown ? 1 : pages.Count;
            // 대화 기록을 펼치면 기록 한 장만 센다.
            int current = isTranscriptShown ? 0 : pageIndex;
            var pagesPerCard = new List<int>();
            string shown = bodyText.text;
            for (int i = 0; i < count; i++)
                pagesPerCard.Add(
                    i == current ? 1 : Mathf.Max(1, bodyText.GetTextInfo(pages[i].Body).pageCount)
                );
            // 다른 카드 쪽수를 재느라 바뀐 글을 지금 카드로 되돌린다.
            bodyText.text = shown;
            bodyText.ForceMeshUpdate();
            int textPages = Mathf.Max(1, bodyText.textInfo.pageCount);
            pagesPerCard[current] = textPages;
            textPage = Mathf.Clamp(textPage, 1, textPages);
            bodyText.pageToDisplay = textPage;
            pageNumberText.text = FeedbackPageNumber.Format(pagesPerCard, current, textPage);
            previousButton.interactable = pageIndex > 0 || textPage > 1;
            nextButton.interactable = pageIndex + 1 < count || textPage < textPages;
        }

        public void SelectSection(FeedbackSection selected)
        {
            section = selected;
            pageIndex = 0;
            textPage = 1;
            pages = FeedbackPages.Create(feedback, section);
            Refresh();
        }

        public void PreviousPage()
        {
            if (textPage > 1)
                textPage--;
            else if (!isTranscriptShown && pageIndex > 0)
                pageIndex--;
            Refresh();
        }

        public void NextPage()
        {
            if (textPage < Mathf.Max(1, bodyText.textInfo.pageCount))
                textPage++;
            else if (!isTranscriptShown && pageIndex + 1 < pages.Count)
            {
                pageIndex++;
                textPage = 1;
            }
            Refresh();
        }

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
            textPage = 1;
            Refresh();
        }
    }
}
