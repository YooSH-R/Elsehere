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

        // 요약 글이 표현 줄 아래에서 시작하도록 띄우는 간격과, 표현 줄이 없을 때 카드 위 여백
        private const float BodyGapBelowRows = 16;
        private const float BodyTopPadding = 32;

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
            titleText.text = feedback.IsFailed ? feedback.Title : "대화 노트";
            summaryText.text = $"{feedback.NpcName}와 나눈 대화";
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
                sectionButtons[i].interactable = i != (int)section;

            // 받지 못한 피드백은 저장할 표현이나 상세 평가가 없으므로 그 버튼 대신 다시 만들기·대화 기록을 보여 준다.
            saveButton.gameObject.SetActive(isSaveAvailable && !feedback.IsFailed);
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
            ShowSituationRows(
                section == FeedbackSection.Summary && !feedback.IsFailed && !isTranscriptShown
                    ? feedback.SituationExpressions
                    : Array.Empty<SituationExpressionData>()
            );
            UpdateLayout();
        }

        /// <summary>
        /// 피드백을 열자마자 보이도록 요약 탭 위쪽에 이 상황에서 자주 쓰는 표현을 한 줄씩 모두 보이고, 줄마다 [+ 담기]로 그 표현과 뜻을 담게 한다.
        /// 요약 글은 표현 줄 아래에서 시작한다.
        /// </summary>
        private void ShowSituationRows(IReadOnlyList<SituationExpressionData> expressions)
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
                row.transform.Find("Texts/Phrase").GetComponent<TMP_Text>().text =
                    $"<b>{expression.Expression}</b>  {expression.MeaningKo}";
                row.transform.Find("Texts/Usage").GetComponent<TMP_Text>().text =
                    expression.UsageKo;
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
                // 카드 크기는 패널의 레이아웃이 정한다. 방금 켠 패널은 레이아웃이 아직 돌지 않아 카드가 원래 크기라서,
                // 그대로 재면 표현 줄이 좁은 너비로 한 줄 높이가 되어 요약 글이 두 줄로 넘어간 표현 줄에 가린다.
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(situationRows);
                top =
                    -situationRows.anchoredPosition.y
                    + situationRows.rect.height
                    + BodyGapBelowRows;
            }
            RectTransform body = bodyText.rectTransform;
            body.offsetMax = new Vector2(body.offsetMax.x, -top);
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
            frame.sizeDelta = new Vector2(
                Mathf.Min(1100, canvas.rect.width - 48),
                Mathf.Min(920, canvas.rect.height - 48)
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
