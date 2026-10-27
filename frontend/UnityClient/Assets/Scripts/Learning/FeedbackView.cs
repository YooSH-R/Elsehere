using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤의 피드백 화면. 짧은 피드백(요약·다음 대화 표현)과 상세 피드백(발화별 참고 표현)을 [상세 보기]로 오간다.
    /// </summary>
    public class FeedbackView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

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

        private ConversationFeedback feedback;
        private bool isDetailShown;

        /// <summary>[닫기]를 누르면 알린다.</summary>
        public event Action CloseRequested;

        /// <summary>[표현 저장]으로 다음 대화 표현의 영어 문장과 한국어 뜻을 알린다.</summary>
        public event Action<string, string> SaveRequested;

        /// <summary>[단어장 보기]를 누르면 알린다.</summary>
        public event Action VocabularyRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show(ConversationFeedback shownFeedback)
        {
            feedback = shownFeedback;
            isDetailShown = false;
            panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            feedback = null;
            panel.SetActive(false);
        }

        private void Awake()
        {
            saveButton.onClick.AddListener(RequestSave);
            detailButton.onClick.AddListener(ToggleDetail);
            vocabularyButton.onClick.AddListener(() => VocabularyRequested?.Invoke());
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            Hide();
        }

        private void Refresh()
        {
            summaryText.text =
                $"{feedback.NpcName}와 나눈 대화 · 내 발화 {feedback.UtteranceCount}개\n{feedback.Summary}";
            bodyText.text = isDetailShown
                ? FeedbackText.Detail(feedback)
                : FeedbackText.Short(feedback);
            detailButtonLabel.text = isDetailShown ? "요약 보기" : "상세 보기";
            saveButton.interactable = feedback.NextExpression != null;
        }

        private void RequestSave()
        {
            ReferenceExpression expression = feedback.NextExpression;
            if (expression != null)
            {
                SaveRequested?.Invoke(expression.English, expression.Korean);
            }
        }

        private void ToggleDetail()
        {
            isDetailShown = !isDetailShown;
            Refresh();
        }
    }
}
