using System;
using System.Collections.Generic;
using System.Text;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 화면. 최근 대사, 안내·상태 문구, 추천 단어, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[표현 저장]·[대화 종료],
    /// 서버 AI 대화의 [누르고 말하기]·[다시 듣기]·[힌트]·[다시 시도]가 있다.
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
        private RectTransform helpPanel;

        [SerializeField]
        private TMP_Text hintText;

        [SerializeField]
        private Button useHintButton;

        [SerializeField]
        private ScrollRect historyScroll;
        private Vector2 lastCanvasSize;

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

        [SerializeField]
        private Button hintButton;

        [SerializeField]
        private Button retryButton;

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

        /// <summary>[뜻 보기]로 뜻을 펼쳤는데 아직 뜻이 없으면 알린다. 서버 대화는 이때 뜻을 받아 온다.</summary>
        public event Action MeaningRequested;

        /// <summary>[힌트]를 누르면 알린다.</summary>
        public event Action HintRequested;

        /// <summary>[다시 시도]를 누르면 알린다.</summary>
        public event Action RetryRequested;

        public bool IsVisible => panel.activeSelf;

        /// <summary>[표현 저장]을 보여 줄지. 저장할 곳이 없으면 끈다.</summary>
        public bool IsSaveAvailable { get; set; } = true;

        public void Show(IDialogueContent shownContent)
        {
            content = shownContent;
            isMeaningVisible = false;
            inputField.text = "";
            panel.SetActive(true);
            helpPanel.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            content = null;
            panel.SetActive(false);
            helpPanel.gameObject.SetActive(false);
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
            statusText.text = FormatStatus();
            suggestText.text = DialogueHelpText.Words(content.SuggestWords);
            string sentence = DialogueHelpText.Sentence(content.HintText, content.ExampleSentence);
            hintText.text = string.IsNullOrEmpty(sentence)
                ? "[힌트]를 누르면 추천 문장을 볼 수 있어요."
                : sentence;
            useHintButton.interactable = content.CanSend && !string.IsNullOrEmpty(sentence);

            sendButton.interactable = content.CanSend;
            // 시작에 실패했거나 끝난 대화에 입력할 수 있으면 [보내기]가 아무 일도 하지 않는 것처럼 보이므로 함께 막는다.
            inputField.interactable = content.CanSend;
            exampleButton.gameObject.SetActive(!string.IsNullOrEmpty(content.ExampleSentence));
            meaningButton.gameObject.SetActive(content.HasMeaning);
            meaningButton.interactable = content.LastNpcLine != null;
            saveButton.gameObject.SetActive(IsSaveAvailable);
            saveButton.interactable = content.LastNpcLine != null;
            talkButton.gameObject.SetActive(content.UsesVoice);
            talkButton.GetComponent<Button>().interactable = content.CanSend;
            replayButton.gameObject.SetActive(content.UsesVoice);
            replayButton.interactable = content.LastNpcLine != null && !content.IsBusy;
            hintButton.gameObject.SetActive(content.HasHint);
            hintButton.interactable =
                content.LastNpcLine != null && content.CanSend && content.HintText == null;
            retryButton.gameObject.SetActive(content.CanRetry);
            UpdateLayout();
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = 0;
        }

        private void Awake()
        {
            endButton.onClick.AddListener(() => EndRequested?.Invoke());
            sendButton.onClick.AddListener(RequestSend);
            inputField.onSubmit.AddListener(_ => RequestSend());
            exampleButton.onClick.AddListener(FillExampleSentence);
            useHintButton.onClick.AddListener(() =>
            {
                if (content != null && content.CanSend)
                    inputField.text =
                        DialogueHelpText.Sentence(content.HintText, content.ExampleSentence) ?? "";
            });
            meaningButton.onClick.AddListener(ToggleMeaning);
            saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
            replayButton.onClick.AddListener(() => ReplayRequested?.Invoke());
            hintButton.onClick.AddListener(() => HintRequested?.Invoke());
            retryButton.onClick.AddListener(RequestRetry);
            talkButton.Pressed += () => TalkPressed?.Invoke();
            talkButton.Released += () => TalkReleased?.Invoke();
            Hide();
        }

        // 입력창에 되돌려 놓은 실패 문장은 [다시 시도]가 그대로 다시 보내므로 비운다.
        private void RequestRetry()
        {
            inputField.text = "";
            RetryRequested?.Invoke();
        }

        private string FormatStatus()
        {
            if (string.IsNullOrEmpty(content.RecoveryGuide))
            {
                return content.StatusMessage ?? "";
            }

            return string.IsNullOrEmpty(content.StatusMessage)
                ? content.RecoveryGuide
                : $"{content.StatusMessage}\n{content.RecoveryGuide}";
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
            if (isMeaningVisible && content.LastNpcLine?.Korean == null)
            {
                MeaningRequested?.Invoke();
            }

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
                    if (content.LastNpcGlossary != null && content.LastNpcGlossary.Count > 0)
                    {
                        text.AppendLine(
                            $"<color={MeaningColor}>단어 풀이 · {FormatGlossary()}</color>"
                        );
                    }
                }
            }

            return text.ToString().TrimEnd();
        }

        private string FormatGlossary()
        {
            var items = new List<string>();
            foreach (GlossaryData item in content.LastNpcGlossary)
            {
                items.Add($"{item.Text} {item.MeaningKo}");
            }

            return string.Join(" · ", items);
        }

        private void LateUpdate()
        {
            if (
                content != null
                && ((RectTransform)panel.transform.parent).rect.size != lastCanvasSize
            )
                UpdateLayout();
        }

        public void UpdateLayout()
        {
            var frame = (RectTransform)panel.transform;
            lastCanvasSize = ((RectTransform)frame.parent).rect.size;
            bool wide = lastCanvasSize.x >= 1200;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1, 0);
            frame.anchoredPosition = new Vector2(-24, 24);
            frame.sizeDelta = new Vector2(
                wide ? Mathf.Min(1320, lastCanvasSize.x - 400) : lastCanvasSize.x - 48,
                560
            );
            helpPanel.anchorMin = helpPanel.anchorMax = helpPanel.pivot = new Vector2(0, 1);
            helpPanel.anchoredPosition = new Vector2(24, -110);
            helpPanel.sizeDelta = new Vector2(
                wide ? 320 : Mathf.Min(320, lastCanvasSize.x * .43f),
                Mathf.Max(
                    180,
                    Mathf.Min(650, wide ? lastCanvasSize.y - 160 : lastCanvasSize.y - 720)
                )
            );
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
