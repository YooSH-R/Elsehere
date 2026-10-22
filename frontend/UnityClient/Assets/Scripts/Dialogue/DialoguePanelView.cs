using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 화면. 최근 대사, 정해진 흐름에서 읽을 대사, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[표현 저장]·[대화 종료]가 있다.
    /// 음성 입력·다시 듣기는 음성 연동 작업에서 붙인다.
    /// </summary>
    public class DialoguePanelView : MonoBehaviour
    {
        private const int VisibleLineCount = 6;
        private const string MeaningColor = "#B8C0CC";

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Button endButton;

        [SerializeField]
        private TMP_Text linesText;

        [SerializeField]
        private TMP_Text guideText;

        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private Button exampleButton;

        [SerializeField]
        private Button meaningButton;

        [SerializeField]
        private Button saveButton;

        private DialogueConversation conversation;
        private bool isMeaningVisible;

        /// <summary>[대화 종료]를 누르면 알린다.</summary>
        public event Action EndRequested;

        /// <summary>[보내기]나 Enter로 입력한 문장을 알린다.</summary>
        public event Action<string> SendRequested;

        /// <summary>[표현 저장]을 누르면 알린다. 저장할 대사는 대화 처리가 정한다.</summary>
        public event Action SaveRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show(DialogueConversation shownConversation)
        {
            conversation = shownConversation;
            isMeaningVisible = false;
            inputField.text = "";
            panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            conversation = null;
            panel.SetActive(false);
        }

        /// <summary>대화 내용이 바뀐 뒤 화면을 다시 그린다.</summary>
        public void Refresh()
        {
            if (conversation == null)
            {
                return;
            }

            linesText.text = FormatRecentLines();
            guideText.text = FormatGuide();
            sendButton.interactable = !conversation.IsGuidedCompleted;
            exampleButton.interactable = !string.IsNullOrEmpty(conversation.ExampleSentence);
            meaningButton.interactable = conversation.LastNpcLine != null;
            saveButton.interactable = conversation.LastNpcLine != null;
        }

        private void Awake()
        {
            endButton.onClick.AddListener(() => EndRequested?.Invoke());
            sendButton.onClick.AddListener(RequestSend);
            inputField.onSubmit.AddListener(_ => RequestSend());
            exampleButton.onClick.AddListener(FillExampleSentence);
            meaningButton.onClick.AddListener(ToggleMeaning);
            saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
            Hide();
        }

        private void RequestSend()
        {
            string text = inputField.text;
            inputField.text = "";
            isMeaningVisible = false;
            SendRequested?.Invoke(text);
        }

        private void FillExampleSentence()
        {
            inputField.text = conversation.ExampleSentence;
        }

        private void ToggleMeaning()
        {
            isMeaningVisible = !isMeaningVisible;
            Refresh();
        }

        private string FormatRecentLines()
        {
            var text = new StringBuilder();
            int firstVisibleIndex = Mathf.Max(0, conversation.Lines.Count - VisibleLineCount);
            for (int i = firstVisibleIndex; i < conversation.Lines.Count; i++)
            {
                DialogueLine line = conversation.Lines[i];
                string speaker = line.IsNpc ? conversation.NpcName : "나";
                text.AppendLine($"<b>{speaker}</b>  {line.English}");

                if (isMeaningVisible && line == conversation.LastNpcLine)
                {
                    text.AppendLine($"<color={MeaningColor}>{line.Korean}</color>");
                }
            }

            return text.ToString().TrimEnd();
        }

        private string FormatGuide()
        {
            if (conversation.IsGuidedCompleted)
            {
                return "길 안내를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요.";
            }

            if (conversation.IsGuided)
            {
                return $"이번에 읽을 대사: <b>{conversation.SuggestedEnglish}</b>\n{conversation.SuggestedKorean}";
            }

            return "정답은 없어요. 하고 싶은 말을 영어로 자유롭게 입력해 보세요.";
        }
    }
}
