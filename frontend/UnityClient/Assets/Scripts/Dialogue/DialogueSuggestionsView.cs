using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>대화창 위의 고정된 단어·추천 문장 카드. 내용만 스크롤하고 문장은 명시적으로 눌러 입력한다.</summary>
    public sealed class DialogueSuggestionsView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform wordsCard;

        [SerializeField]
        private RectTransform hintCard;

        [SerializeField]
        private TMP_Text wordsText;

        [SerializeField]
        private TMP_Text hintText;

        [SerializeField]
        private GameObject hintCover;

        [SerializeField]
        private ScrollRect hintScroll;

        [SerializeField]
        private Button hideButton;

        [SerializeField]
        private Button useButton;

        // 추천 단어 한 줄과 단어 사이 구분선의 본. 숨겨 두고 단어 수만큼 복제한다.
        [SerializeField]
        private TMP_Text wordTemplate;

        [SerializeField]
        private GameObject dividerTemplate;

        private readonly List<TMP_Text> wordRows = new List<TMP_Text>();
        private readonly List<GameObject> dividers = new List<GameObject>();

        public event Action HideRequested;
        public event Action SentenceSelected;
        public RectTransform WordsRect => wordsCard;

        /// <summary>추천 문장 카드. [힌트]가 이 안에 있다.</summary>
        public RectTransform HintRect => hintCard;

        private void Awake()
        {
            hideButton.onClick.AddListener(() => HideRequested?.Invoke());
            useButton.onClick.AddListener(() => SentenceSelected?.Invoke());
        }

        public void Show(
            IReadOnlyList<string> words,
            bool isSuggestingWords,
            bool hasHint,
            bool visible,
            string sentence,
            bool pending,
            string meaningKo = null
        )
        {
            ShowWords(DialogueHelpText.WordEntries(words), isSuggestingWords);
            hintCard.gameObject.SetActive(hasHint);
            hintCover.SetActive(!visible);
            hintScroll.gameObject.SetActive(visible);
            hideButton.gameObject.SetActive(visible);
            hintText.text = pending
                ? "문장을 고르고 있어요…"
                : DialogueHelpText.Sentence(sentence, meaningKo);
            if (hintScroll.verticalScrollbar != null)
                hintScroll.verticalScrollbar.gameObject.SetActive(visible);
            useButton.interactable = visible && !pending && !string.IsNullOrWhiteSpace(sentence);
        }

        // 단어마다 줄을 따로 두고 단어 사이에만 얇은 구분선을 넣어, 빈 줄로 공간을 버리지 않는다.
        // 단어가 없으면 고르는 중인지 아직 없는지 알리는 문구만 보인다.
        private void ShowWords(string[] entries, bool isSuggesting)
        {
            wordsText.gameObject.SetActive(entries.Length == 0);
            wordsText.text = entries.Length == 0 ? DialogueHelpText.Words(null, isSuggesting) : "";
            Transform list = wordTemplate.transform.parent;
            while (wordRows.Count < entries.Length)
            {
                GameObject divider = Instantiate(dividerTemplate, list);
                divider.name = "Divider";
                dividers.Add(divider);
                TMP_Text row = Instantiate(wordTemplate, list);
                row.name = "Word";
                wordRows.Add(row);
            }
            for (int i = 0; i < wordRows.Count; i++)
            {
                bool shown = i < entries.Length;
                wordRows[i].gameObject.SetActive(shown);
                dividers[i].SetActive(shown && i > 0);
                if (shown)
                    wordRows[i].text = entries[i];
            }
        }

        public void SetBounds(Vector2 offset, float width, float height)
        {
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = offset;
            root.sizeDelta = new Vector2(width, height);
            float gap = hintCard.gameObject.activeSelf ? 12 : 0;
            float wordsHeight = hintCard.gameObject.activeSelf ? (height - gap) * .6f : height;
            Place(wordsCard, 0, width, Mathf.Max(0, wordsHeight));
            Place(hintCard, wordsHeight + gap, width, Mathf.Max(0, height - wordsHeight - gap));
        }

        private static void Place(RectTransform rect, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
