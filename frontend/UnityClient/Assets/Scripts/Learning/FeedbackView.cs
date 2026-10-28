using System;
using System.Collections.Generic;
using A305.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤의 피드백 화면. 짧은 피드백(요약·다음 대화 표현)과 상세 피드백(발화별 참고 표현)을 [상세 보기]로 오간다.
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
        private Button regenerateButton;

        [SerializeField]
        private Button transcriptButton;

        [SerializeField]
        private TMP_Text transcriptButtonLabel;

        private ConversationFeedback feedback;
        private IReadOnlyList<DialogueLine> lines;
        private bool isDetailShown;
        private bool isTranscriptShown;
        private bool isSaveAvailable = true;

        /// <summary>[닫기]를 누르면 알린다.</summary>
        public event Action CloseRequested;

        /// <summary>[표현 저장]으로 다음 대화 표현의 영어 문장과 한국어 뜻을 알린다.</summary>
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
            isDetailShown = false;
            isTranscriptShown = false;
            panel.SetActive(true);
            Refresh();
        }

        /// <summary>[다시 만들기]를 누른 뒤 새 피드백을 기다리는 동안 보여 준다.</summary>
        public void ShowRegenerating()
        {
            isTranscriptShown = false;
            bodyText.text = "피드백을 다시 만들고 있어요. 잠시만 기다려 주세요.";
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
            regenerateButton.onClick.AddListener(() => RegenerateRequested?.Invoke());
            transcriptButton.onClick.AddListener(ToggleTranscript);
            Hide();
        }

        private void Refresh()
        {
            titleText.text = feedback.Title;
            summaryText.text =
                $"{feedback.NpcName}와 나눈 대화 · 내 발화 {feedback.UtteranceCount}개\n{feedback.Summary}";
            bodyText.text = Body();
            detailButtonLabel.text = isDetailShown ? "요약 보기" : "상세 보기";
            transcriptButtonLabel.text = isTranscriptShown ? "기록 닫기" : "대화 기록 보기";

            // 받지 못한 피드백은 저장할 표현이나 상세 평가가 없으므로 그 버튼 대신 다시 만들기·대화 기록을 보여 준다.
            saveButton.gameObject.SetActive(isSaveAvailable && !feedback.IsFailed);
            saveButton.interactable = feedback.NextExpression != null;
            detailButton.gameObject.SetActive(!feedback.IsFailed);
            regenerateButton.gameObject.SetActive(feedback.CanRegenerate);
            regenerateButton.interactable = true;
            transcriptButton.gameObject.SetActive(feedback.IsFailed);
            transcriptButton.interactable = true;
        }

        private string Body()
        {
            if (isTranscriptShown)
            {
                return FeedbackText.Transcript(feedback.NpcName, lines);
            }

            // 받지 못한 피드백에 "권할 표현이 없어요" 같은 평가 문구를 붙이지 않는다. 이유는 요약 줄에 있다.
            if (feedback.IsFailed)
            {
                return "";
            }

            return isDetailShown ? FeedbackText.Detail(feedback) : FeedbackText.Short(feedback);
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

        private void ToggleTranscript()
        {
            isTranscriptShown = !isTranscriptShown;
            Refresh();
        }
    }
}
