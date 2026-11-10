using System;
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

        public event Action HideRequested;
        public event Action SentenceSelected;
        public RectTransform WordsRect => wordsCard;

        private void Awake()
        {
            hideButton.onClick.AddListener(() => HideRequested?.Invoke());
            useButton.onClick.AddListener(() => SentenceSelected?.Invoke());
        }

        public void Show(
            string words,
            bool hasHint,
            bool visible,
            string sentence,
            bool pending,
            string meaningKo = null
        )
        {
            wordsText.text = words;
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
