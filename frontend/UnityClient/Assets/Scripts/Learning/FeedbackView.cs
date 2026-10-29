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
            UpdateLayout();
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

        public void UpdateLayout()
        {
            var frame = (RectTransform)panel.transform;
            var canvas = (RectTransform)frame.parent;
            lastCanvasSize = canvas.rect.size;
            frame.sizeDelta = new Vector2(
                Mathf.Min(1100, canvas.rect.width - 48),
                Mathf.Min(920, canvas.rect.height - 48)
            );
            if (feedback == null || !panel.activeInHierarchy)
                return;
            Canvas.ForceUpdateCanvases();
            bodyText.ForceMeshUpdate();
            int textPages = Mathf.Max(1, bodyText.textInfo.pageCount);
            textPage = Mathf.Clamp(textPage, 1, textPages);
            bodyText.pageToDisplay = textPage;
            int count = isTranscriptShown ? 1 : pages.Count;
            pageNumberText.text =
                $"{pageIndex + 1} / {count}"
                + (textPages > 1 ? $"  ·  {textPage}/{textPages}" : "");
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
