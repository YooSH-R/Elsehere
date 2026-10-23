using System;
using System.Text;
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
            bodyText.text = isDetailShown ? FormatDetail() : FormatShort();
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

        private string FormatShort()
        {
            var text = new StringBuilder();
            ReferenceExpression expression = feedback.NextExpression;
            if (expression == null)
            {
                text.AppendLine(
                    "이번 대화에서는 따로 권할 표현이 없어요. 지금처럼 말해도 자연스러워요."
                );
            }
            else
            {
                text.AppendLine("다음 대화에서 활용할 표현");
                text.AppendLine($"<b>{expression.English}</b>");
                text.AppendLine(expression.Korean);
            }

            text.AppendLine();
            text.Append("피드백 예시 · 실제 AI 분석이나 실력 평가가 아니에요.");
            return text.ToString();
        }

        private string FormatDetail()
        {
            var text = new StringBuilder();
            for (int i = 0; i < feedback.Reviews.Count; i++)
            {
                UtteranceReview review = feedback.Reviews[i];
                text.AppendLine($"{i + 1:00} 내가 말한 문장 · <b>{review.Utterance}</b>");
                if (review.Reference == null)
                {
                    text.AppendLine("참고 표현 없음 · 지금 문장 그대로도 자연스럽게 전달돼요.");
                }
                else
                {
                    text.AppendLine(
                        $"참고 표현 · <b>{review.Reference.English}</b> ({review.Reference.Korean})"
                    );
                    text.AppendLine(review.Reference.Reason);
                }

                text.AppendLine();
            }

            text.Append("문법 오류나 점수를 매기지 않아요. 상황에 맞는 참고 표현만 보여 드려요.");
            return text.ToString();
        }
    }
}
