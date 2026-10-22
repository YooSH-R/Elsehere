using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 화면 오른쪽 위의 [단어장] 버튼과 저장 알림. 알림은 잠깐 보였다 사라진다.
    /// </summary>
    public class LearningHudView : MonoBehaviour
    {
        private const float MessageSeconds = 2.5f;

        [SerializeField]
        private Button vocabularyButton;

        [SerializeField]
        private TMP_Text vocabularyButtonLabel;

        [SerializeField]
        private TMP_Text messageText;

        private float messageHideTime;

        public event Action VocabularyRequested;

        public void SetVocabularyCount(int count)
        {
            vocabularyButtonLabel.text = $"단어장 {count}";
        }

        /// <summary>대화 중에는 단어장을 열 수 없게 한다. 단어장의 이동 잠금이 대화의 이동 잠금을 풀지 않게 하기 위함이다.</summary>
        public void SetVocabularyAvailable(bool isAvailable)
        {
            vocabularyButton.interactable = isAvailable;
        }

        public void ShowMessage(string message)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            messageHideTime = Time.unscaledTime + MessageSeconds;
        }

        private void Awake()
        {
            vocabularyButton.onClick.AddListener(() => VocabularyRequested?.Invoke());
            messageText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (messageText.gameObject.activeSelf && Time.unscaledTime >= messageHideTime)
            {
                messageText.gameObject.SetActive(false);
            }
        }
    }
}
