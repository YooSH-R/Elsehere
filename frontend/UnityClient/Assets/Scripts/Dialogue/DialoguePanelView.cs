using System;
using System.Text;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 화면. 최근 대사, 안내·상태 문구, 추천 단어, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[표현 저장]·[대화 종료],
    /// 서버 AI 대화의 [누르고 말하기]·[다시 듣기]가 있다.
    /// </summary>
    public class DialoguePanelView : MonoBehaviour
    {
        private const int VisibleLineCount = 6;
        private const string MeaningColor = "#B8C0CC";
        private const string FreeTalkGuide =
            "정답은 없어요. 영어로 자유롭게 말하거나 입력해 보세요. 한국어를 섞어도 돼요.";

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Button endButton;

        [SerializeField]
        private TMP_Text linesText;

        [SerializeField]
        private TMP_Text guideText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private TMP_Text suggestText;

        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private Button exampleButton;

        [SerializeField]
        private HoldButton talkButton;

        [SerializeField]
        private Button meaningButton;

        [SerializeField]
        private Button saveButton;

        [SerializeField]
        private Button replayButton;

        private IDialogueContent content;
        private bool isMeaningVisible;

        /// <summary>[대화 종료]를 누르면 알린다.</summary>
        public event Action EndRequested;

        /// <summary>[보내기]나 Enter로 입력한 문장을 알린다.</summary>
        public event Action<string> SendRequested;

        /// <summary>[표현 저장]을 누르면 알린다. 저장할 대사는 대화 처리가 정한다.</summary>
        public event Action SaveRequested;

        /// <summary>[누르고 말하기]를 누르기 시작하면 알린다.</summary>
        public event Action TalkPressed;

        /// <summary>[누르고 말하기]에서 손을 떼면 알린다.</summary>
        public event Action TalkReleased;

        /// <summary>[다시 듣기]를 누르면 알린다.</summary>
        public event Action ReplayRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show(IDialogueContent shownContent)
        {
            content = shownContent;
            isMeaningVisible = false;
            inputField.text = "";
            panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            content = null;
            panel.SetActive(false);
        }

        /// <summary>전사한 문장처럼 입력창에 넣어 줄 문장. 사용자가 확인하고 [보내기]로 보낸다.</summary>
        public void SetInputText(string text)
        {
            inputField.text = text;
        }

        /// <summary>대화 내용이 바뀐 뒤 화면을 다시 그린다.</summary>
        public void Refresh()
        {
            if (content == null)
            {
                return;
            }

            // 보내지 못한 문장은 입력창에 되돌려 놓아 그대로 다시 보낼 수 있게 한다.
            if (content is ServerConversation server && string.IsNullOrEmpty(inputField.text))
            {
                string failed = server.TakeTextToRestore();
                if (!string.IsNullOrEmpty(failed))
                {
                    inputField.text = failed;
                }
            }

            linesText.text = FormatRecentLines();
            guideText.text = FormatGuide();
            statusText.text = content.StatusMessage ?? "";
            suggestText.text =
                content.SuggestWords != null && content.SuggestWords.Count > 0
                    ? "추천 단어 · " + string.Join(" · ", content.SuggestWords)
                    : "";

            sendButton.interactable = content.CanSend;
            inputField.interactable = !content.IsBusy;
            exampleButton.gameObject.SetActive(!string.IsNullOrEmpty(content.ExampleSentence));
            meaningButton.gameObject.SetActive(content.HasMeaning);
            meaningButton.interactable = content.LastNpcLine != null;
            saveButton.interactable = content.LastNpcLine != null;
            talkButton.gameObject.SetActive(content.UsesVoice);
            talkButton.GetComponent<Button>().interactable = content.CanSend;
            replayButton.gameObject.SetActive(content.UsesVoice);
            replayButton.interactable = content.LastNpcLine != null && !content.IsBusy;
        }

        private void Awake()
        {
            endButton.onClick.AddListener(() => EndRequested?.Invoke());
            sendButton.onClick.AddListener(RequestSend);
            inputField.onSubmit.AddListener(_ => RequestSend());
            exampleButton.onClick.AddListener(FillExampleSentence);
            meaningButton.onClick.AddListener(ToggleMeaning);
            saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
            replayButton.onClick.AddListener(() => ReplayRequested?.Invoke());
            talkButton.Pressed += () => TalkPressed?.Invoke();
            talkButton.Released += () => TalkReleased?.Invoke();
            Hide();
        }

        private void RequestSend()
        {
            if (content == null || !content.CanSend)
            {
                return;
            }

            string text = inputField.text;
            inputField.text = "";
            isMeaningVisible = false;
            SendRequested?.Invoke(text);
        }

        private void FillExampleSentence()
        {
            inputField.text = content.ExampleSentence;
        }

        private void ToggleMeaning()
        {
            isMeaningVisible = !isMeaningVisible;
            Refresh();
        }

        private string FormatRecentLines()
        {
            var text = new StringBuilder();
            int firstVisibleIndex = Mathf.Max(0, content.Lines.Count - VisibleLineCount);
            for (int i = firstVisibleIndex; i < content.Lines.Count; i++)
            {
                DialogueLine line = content.Lines[i];
                string speaker = line.IsNpc ? content.NpcName : "나";
                text.AppendLine($"<b>{speaker}</b>  {line.English}");

                if (isMeaningVisible && line == content.LastNpcLine && line.Korean != null)
                {
                    text.AppendLine($"<color={MeaningColor}>{line.Korean}</color>");
                }
            }

            return text.ToString().TrimEnd();
        }

        private string FormatGuide()
        {
            if (content.IsGuidedCompleted)
            {
                return "길 안내를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요.";
            }

            if (content.IsGuided)
            {
                return $"이번에 읽을 대사: <b>{content.SuggestedEnglish}</b>\n{content.SuggestedKorean}";
            }

            return content.UsesVoice
                ? FreeTalkGuide + " [누르고 말하기]를 누른 채 말하면 문장으로 바꿔 드려요."
                : "정답은 없어요. 하고 싶은 말을 영어로 자유롭게 입력해 보세요.";
        }
    }
}
