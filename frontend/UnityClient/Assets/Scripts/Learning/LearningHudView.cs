using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 화면 오른쪽 위의 [지난 대화]·[단어장] 버튼과 저장 알림. 알림은 잠깐 보였다 사라진다.
    /// [지난 대화] 창도 이 HUD에 있어 장소 씬마다 배치하지 않는다.
    /// </summary>
    public class LearningHudView : MonoBehaviour
    {
        private const float MessageSeconds = 2.5f;

        [SerializeField]
        private Button historyButton;

        [SerializeField]
        private ConversationHistoryView historyView;

        [SerializeField]
        private Button vocabularyButton;

        [SerializeField]
        private Button homeButton;

        [SerializeField]
        private TMP_Text vocabularyButtonLabel;

        [SerializeField]
        [Tooltip("아이콘 버튼 위 숫자 배지의 글자. 비워 두면 버튼 이름에 개수를 붙인다.")]
        private TMP_Text vocabularyCountText;

        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        [Tooltip("알림과 함께 보이고 숨는 바탕. 비워 두면 알림 글자만 보이고 숨는다.")]
        private GameObject messageBox;

        private float messageHideTime;

        public event Action HistoryRequested;

        public event Action VocabularyRequested;

        public event Action HomeRequested;

        public ConversationHistoryView HistoryView => historyView;

        public void SetHomeAvailable(bool available) => homeButton.interactable = available;

        /// <summary>숫자 배지가 있으면 버튼 이름은 그대로 두고 배지에 개수를 보인다. 모은 표현이 없으면 배지를 숨긴다.</summary>
        public void SetVocabularyCount(int count)
        {
            if (vocabularyCountText == null)
            {
                vocabularyButtonLabel.text = $"단어장 {count}";
                return;
            }
            vocabularyButtonLabel.text = "단어장";
            vocabularyCountText.text = count.ToString();
            vocabularyCountText.transform.parent.gameObject.SetActive(count > 0);
        }

        /// <summary>
        /// 대화 중에는 단어장과 지난 대화를 열 수 없게 한다. 두 화면의 이동 잠금이 대화의 이동 잠금을 풀지 않게 하기 위함이다.
        /// </summary>
        public void SetVocabularyAvailable(bool isAvailable)
        {
            vocabularyButton.interactable = isAvailable;
            historyButton.interactable = isAvailable;
        }

        public void ShowMessage(string message)
        {
            messageText.text = message;
            MessageBox.SetActive(true);
            messageHideTime = Time.unscaledTime + MessageSeconds;
        }

        private void Awake()
        {
            historyButton.onClick.AddListener(() => HistoryRequested?.Invoke());
            vocabularyButton.onClick.AddListener(() => VocabularyRequested?.Invoke());
            homeButton.onClick.AddListener(() => HomeRequested?.Invoke());
            MessageBox.SetActive(false);
        }

        private GameObject MessageBox => messageBox != null ? messageBox : messageText.gameObject;

        private void Update()
        {
            if (MessageBox.activeSelf && Time.unscaledTime >= messageHideTime)
            {
                MessageBox.SetActive(false);
            }
        }
    }
}
