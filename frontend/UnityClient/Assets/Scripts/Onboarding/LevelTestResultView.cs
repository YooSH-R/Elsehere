using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>화면 04의 결과(L4): 추천 난이도, 듣기·말하기 점수, 한 줄 피드백(없으면 숨김), [다음].</summary>
    public class LevelTestResultView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text levelText;

        [SerializeField]
        private TMP_Text listeningScoreText;

        [SerializeField]
        private TMP_Text speakingScoreText;

        [SerializeField]
        [Tooltip("피드백 칸과 위 간격. 피드백이 없으면 통째로 숨긴다")]
        private GameObject feedbackSlot;

        [SerializeField]
        private TMP_Text feedbackText;

        [SerializeField]
        private Button nextButton;

        public event Action NextRequested;

        private void Awake()
        {
            nextButton.onClick.AddListener(() => NextRequested?.Invoke());
        }

        public void Show(LevelTestSession test)
        {
            levelText.text = test.RecommendedLevelName;
            listeningScoreText.text = test.ListeningScoreText;
            speakingScoreText.text = test.SpeakingScoreText;
            string feedback = test.Result.Feedback;
            feedbackSlot.SetActive(!string.IsNullOrEmpty(feedback));
            feedbackText.text = feedback ?? "";
        }
    }
}
